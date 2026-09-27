// Checks of the logistic and Poisson fit in StatsDirectUI/Builtins/Regress1.cs, by calculation: that the decomposition routines leave in their arrays what
// their comments say, and that each fit returns the coefficients, covariance matrix, leverages and deviance residuals of its model, with and without a
// constant, prior weights and unselected predictors.  Regress1.cs is compiled on its own; its private routines are reached by reflection.
using System.Reflection;
using StatsDirect.Builtins;

internal static partial class Program
{
    private static int failures;
    private const BindingFlags Any = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    private static object Call(string name, params object[] args)
    {
        MethodInfo method = typeof(Regress1).GetMethod(name, Any) ?? throw new Exception("no method " + name);
        return method.Invoke(null, args);
    }

    private static void Check(string what, double worst, double tolerance)
    {
        bool ok = worst <= tolerance;
        if (!ok) failures++;
        Console.WriteLine($"{(ok ? "ok  " : "FAIL")}  {what}: worst difference {worst:E2} (tolerance {tolerance:E0})");
    }

    // ---- small dense linear algebra, 0-based, for the independent side of each comparison ----
    private static double[,] Inverse(double[,] a)
    {
        int n = a.GetLength(0);
        double[,] w = new double[n, 2 * n];
        for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) w[i, j] = a[i, j]; w[i, n + i] = 1; }
        for (int c = 0; c < n; c++)
        {
            int p = c;
            for (int r = c + 1; r < n; r++) if (Math.Abs(w[r, c]) > Math.Abs(w[p, c])) p = r;
            for (int j = 0; j < 2 * n; j++) (w[c, j], w[p, j]) = (w[p, j], w[c, j]);
            double d = w[c, c];
            for (int j = 0; j < 2 * n; j++) w[c, j] /= d;
            for (int r = 0; r < n; r++)
                if (r != c) { double f = w[r, c]; for (int j = 0; j < 2 * n; j++) w[r, j] -= f * w[c, j]; }
        }
        double[,] inv = new double[n, n];
        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) inv[i, j] = w[i, n + j];
        return inv;
    }

    private static double[,] Data(int records, int predictors, int seed)
    {
        // a fixed, well conditioned design: 1-based, x[i, j]
        System.Random random = new(seed);
        double[,] x = new double[records + 1, predictors + 1];
        for (int i = 1; i <= records; i++)
            for (int j = 1; j <= predictors; j++)
                x[i, j] = Math.Round(random.NextDouble() * 4 - 2, 3) + (j == 2 ? 0.3 * x[i, 1] : 0);
        return x;
    }

    private static int Main()
    {
        Decompositions();
        double[] logisticLeverage = Fit("logistic, constant and 3 predictors, all selected", true, 3, new[] { false, true, true, true }, false, null);
        double[] withSpare = Fit("logistic, constant and 3 of 5 predictors selected (2 left out)", true, 5, new[] { false, true, true, true, false, false }, false, null);
        double worst = 0;
        for (int i = 1; i < logisticLeverage.Length; i++) worst = Math.Max(worst, Math.Abs(logisticLeverage[i] - withSpare[i]));
        Check("leverage the same whether or not unselected predictors are present", worst, 1e-10);
        double[] oneSpare = Fit("logistic, constant and 3 of 4 predictors selected (1 left out)", true, 4, new[] { false, true, true, true, false }, false, null);
        worst = 0;
        for (int i = 1; i < logisticLeverage.Length; i++) worst = Math.Max(worst, Math.Abs(logisticLeverage[i] - oneSpare[i]));
        Check("leverage the same with one unselected predictor present", worst, 1e-10);
        Fit("logistic, no constant, 3 predictors", false, 3, new[] { false, true, true, true }, false, null);
        Fit("logistic, prior weights (some 2, one 0)", true, 3, new[] { false, true, true, true }, false, new double[] { 0, 1, 2, 1, 1, 2, 0, 1, 1, 2, 1, 1, 1, 2, 1, 1 });
        Fit("Poisson, constant and 3 predictors", true, 3, new[] { false, true, true, true }, true, null);
        Fit("Poisson, prior weights (some 2, one 0)", true, 3, new[] { false, true, true, true }, true, new double[] { 0, 1, 2, 1, 1, 2, 0, 1, 1, 2, 1, 1, 1, 2, 1, 1 });
        LinearRegressionChecks();
        Console.WriteLine(failures == 0 ? "ALL CHECKS PASS" : failures + " CHECK(S) FAILED");
        return failures == 0 ? 0 : 1;
    }

    private static void Decompositions()
    {
        const int m = 9, n = 4;
        double[,] source = Data(m, n, 11);
        double[,] a = new double[m + 1, m + 1];
        for (int i = 1; i <= m; i++) for (int j = 1; j <= n; j++) a[i, j] = source[i, j];
        double[] b = new double[m + 1];
        for (int i = 1; i <= m; i++) b[i] = Math.Sin(i) + 0.5 * source[i, 1] - source[i, 3];
        double[] bIn = (double[])b.Clone();

        // A'A, A'b and the least squares solution by the normal equations
        double[,] ata = new double[n, n]; double[] atb = new double[n];
        for (int j = 0; j < n; j++)
        {
            for (int k = 0; k < n; k++) for (int i = 1; i <= m; i++) ata[j, k] += a[i, j + 1] * a[i, k + 1];
            for (int i = 1; i <= m; i++) atb[j] += a[i, j + 1] * b[i];
        }
        double[,] ataInverse = Inverse(ata);
        double[] solution = new double[n];
        for (int j = 0; j < n; j++) for (int k = 0; k < n; k++) solution[j] += ataInverse[j, k] * atb[k];
        double residualSquares = 0;
        for (int i = 1; i <= m; i++) { double fitted = 0; for (int j = 1; j <= n; j++) fitted += a[i, j] * solution[j - 1]; residualSquares += (b[i] - fitted) * (b[i] - fitted); }

        double[] zeta = new double[2 * m + 1];
        Call("QRFactorization", m, n, a, zeta);

        // R'R = A'A, with R the upper triangle
        double worst = 0;
        for (int j = 1; j <= n; j++)
            for (int k = 1; k <= n; k++)
            {
                double sum = 0;
                for (int i = 1; i <= Math.Min(j, k); i++) sum += a[i, j] * a[i, k];
                worst = Math.Max(worst, Math.Abs(sum - ata[j - 1, k - 1]));
            }
        Check("QRFactorization: R'R = A'A with R in the upper triangle", worst, 1e-12);
        bool below = false;
        for (int i = 2; i <= m; i++) for (int j = 1; j < Math.Min(i, n + 1); j++) if (a[i, j] != 0) below = true;
        Console.WriteLine("      below the diagonal is " + (below ? "not zero: the reflection vectors are kept there" : "zero"));
        Console.WriteLine("      zeta (one scalar for each reflection): " + string.Join(", ", zeta.Skip(1).Take(n).Select(z => z.ToString("F4"))));

        // each reflection is H = I - u u' with u = (zeta, the column below the diagonal): u'u = 2, and H H = I then follows
        worst = 0;
        for (int j = 1; j <= n; j++)
        {
            double uu = zeta[j] * zeta[j];
            for (int i = j + 1; i <= m; i++) uu += a[i, j] * a[i, j];
            worst = Math.Max(worst, Math.Abs(uu - 2));
        }
        Check("each reflection vector u = (zeta, column below the diagonal) has u'u = 2", worst, 1e-12);
        // the diagonal of R has the sign opposite to the element it replaced
        Console.WriteLine("      diagonal of R: " + string.Join(", ", Enumerable.Range(1, n).Select(j => a[j, j].ToString("F4"))) + "   first column of A began with " + source[1, 1].ToString("F4"));

        Call("X_Householder_QR_Transformation", m, n, a, zeta, b);
        double normIn = Math.Sqrt(bIn.Skip(1).Sum(v => v * v)), normOut = Math.Sqrt(b.Skip(1).Sum(v => v * v));
        Check("Householder transformation keeps the length of b", Math.Abs(normIn - normOut), 1e-12);
        // back substitution R x = (Q'b)[1..n]
        double[] x = new double[n + 1];
        for (int i = n; i >= 1; i--) { double sum = b[i]; for (int j = i + 1; j <= n; j++) sum -= a[i, j] * x[j]; x[i] = sum / a[i, i]; }
        worst = 0; for (int j = 1; j <= n; j++) worst = Math.Max(worst, Math.Abs(x[j] - solution[j - 1]));
        Check("R x = first n elements of the transformed b gives the least squares solution (so the transform is Q'b)", worst, 1e-12);
        double tail = 0; for (int i = n + 1; i <= m; i++) tail += b[i] * b[i];
        Check("the remaining elements of Q'b hold the residual sum of squares", Math.Abs(tail - residualSquares), 1e-12);

        // R'R before the SVD overwrites R
        double[,] rtr = new double[n + 1, n + 1];
        for (int j = 1; j <= n; j++) for (int k = 1; k <= n; k++) for (int i = 1; i <= Math.Min(j, k); i++) rtr[j, k] += a[i, j] * a[i, k];

        double[] sv = zeta; int errLevel = 0; string errMsg = "";
        object[] args = { n, a, m, 1, b, sv, errLevel, errMsg };
        Call("X_SVD_Regression_Main", args);
        Console.WriteLine("      singular values: " + string.Join(", ", sv.Skip(1).Take(n).Select(z => z.ToString("F6"))) + "  error level " + args[6]);
        bool descending = true; for (int j = 2; j <= n; j++) if (sv[j] > sv[j - 1]) descending = false;
        Check("singular values are positive and in descending order", descending && sv[n] > 0 ? 0 : 1, 0);
        // rows of the array are the rows of P': P'P = I and P'(R'R)P = S^2
        worst = 0; double worstDiagonal = 0;
        for (int j = 1; j <= n; j++)
            for (int k = 1; k <= n; k++)
            {
                double dot = 0, quad = 0;
                for (int i = 1; i <= n; i++) dot += a[j, i] * a[k, i];
                for (int i = 1; i <= n; i++) for (int l = 1; l <= n; l++) quad += a[j, i] * rtr[i, l] * a[k, l];
                worst = Math.Max(worst, Math.Abs(dot - (j == k ? 1 : 0)));
                worstDiagonal = Math.Max(worstDiagonal, Math.Abs(quad - (j == k ? sv[j] * sv[j] : 0)));
            }
        Check("the array holds P' by rows: its rows are orthonormal", worst, 1e-12);
        Check("P'(R'R)P = S^2, so R = U S P' with these singular values", worstDiagonal, 1e-11);
        // solution = sum over j of (c[j] / s[j]) * row j of P'
        worst = 0;
        for (int i = 1; i <= n; i++)
        {
            double sum = 0; for (int j = 1; j <= n; j++) sum += b[j] / sv[j] * a[j, i];
            worst = Math.Max(worst, Math.Abs(sum - solution[i - 1]));
        }
        Check("b = P S^-1 c, with c the right-hand side after both transforms, is the least squares solution (so c = U'Q'b)", worst, 1e-11);
    }

    private static double[] Fit(string title, bool constant, int predictors, bool[] select, bool poisson, double[] prior)
    {
        Console.WriteLine();
        Console.WriteLine(title);
        const int records = 15;
        double[,] x = Data(records, 5, 7);   // the same first columns whatever the number of predictors
        double[,] xUsed = new double[records + 1, predictors + 1];
        for (int i = 1; i <= records; i++) for (int j = 1; j <= predictors; j++) xUsed[i, j] = x[i, j];
        int selected = select.Count(s => s);
        int p = selected + (constant ? 1 : 0);
        double[] y = new double[records + 1], t = new double[records + 1], weight = new double[records + 1], offset = new double[records + 1];
        for (int i = 1; i <= records; i++)
        {
            t[i] = 10 + i % 4;
            double eta = (constant ? 0.3 : 0) + 0.5 * x[i, 1] - 0.4 * x[i, 2] + 0.2 * x[i, 3];
            y[i] = poisson ? Math.Round(Math.Exp(1 + eta) + (i % 3 - 1)) : Math.Round(t[i] / (1 + Math.Exp(-eta)) + (i % 3 - 1) * 0.8);
            if (y[i] < 0) y[i] = 0;
            if (!poisson && y[i] > t[i]) y[i] = t[i];
            weight[i] = prior == null ? 1 : prior[i];
        }
        bool useWeights = prior != null;
        double[] beta = new double[p + 1], se = new double[p + 1], covariance = new double[p * (p + 1) / 2 + 1];
        double[] fit = new double[records + 1], residual = new double[records + 1], leverage = new double[records + 1];
        double deviance = 0; int df = 0, rank = 0, errLevel; string dropped = "", errMsg = "";
        if (poisson)
            Regress1.X_Poisson_Regression(constant, false, ref useWeights, records, xUsed, predictors, select, p, y, t, weight, ref deviance, ref df, beta, ref rank, se, covariance, 1e-12, 50, fit, residual, leverage, offset, out errLevel, ref dropped, ref errMsg);
        else
            Regress1.X_Logistic_Regression(constant, false, ref useWeights, records, xUsed, predictors, select, p, y, t, weight, out deviance, ref df, beta, ref rank, se, covariance, 1e-12, 50, fit, residual, leverage, offset, out errLevel, ref dropped, ref errMsg);
        Console.WriteLine($"      error level {errLevel} {errMsg} {dropped}; rank {rank}, df {df}, deviance {deviance:F6}; coefficients " + string.Join(", ", beta.Skip(1).Select(v => v.ToString("F6"))));

        // the design matrix as fitted, 0-based columns: constant first, then the selected predictors in order
        double[,] d = new double[records + 1, p];
        for (int i = 1; i <= records; i++)
        {
            int k = 0;
            if (constant) d[i, k++] = 1;
            for (int j = 1; j <= predictors; j++) if (select[j]) d[i, k++] = x[i, j];
        }
        double[] mu = new double[records + 1], w = new double[records + 1];
        for (int i = 1; i <= records; i++)
        {
            double eta = 0; for (int k = 0; k < p; k++) eta += d[i, k] * beta[k + 1];
            if (poisson) { mu[i] = Math.Exp(eta); w[i] = mu[i]; }
            else { double pr = 1 / (1 + Math.Exp(-eta)); mu[i] = t[i] * pr; w[i] = t[i] * pr * (1 - pr); }
            w[i] *= weight[i];
        }
        double worst = 0;
        for (int i = 1; i <= records; i++) if (weight[i] > 0) worst = Math.Max(worst, Math.Abs(mu[i] - fit[i]));
        Check("fitted values are mu = n p (logistic) or exp(eta) (Poisson) at the returned coefficients", worst, 1e-8);
        worst = 0;
        for (int k = 0; k < p; k++) { double score = 0; for (int i = 1; i <= records; i++) score += weight[i] * d[i, k] * (y[i] - mu[i]); worst = Math.Max(worst, Math.Abs(score)); }
        Check("score equations X'(prior weight * (y - mu)) = 0 at the returned coefficients", worst, 1e-6);

        double[,] xwx = new double[p, p];
        for (int j = 0; j < p; j++) for (int k = 0; k < p; k++) for (int i = 1; i <= records; i++) xwx[j, k] += w[i] * d[i, j] * d[i, k];
        double[,] inverse = Inverse(xwx);
        worst = 0;
        for (int i = 1; i <= p; i++)
            for (int k = 1; k <= i; k++)
                worst = Math.Max(worst, Math.Abs(covariance[i * (i - 1) / 2 + k] - inverse[i - 1, k - 1]));
        Check("covariance = (X'WX)^-1, packed by rows of the lower triangle: element (i, k) at i(i-1)/2 + k", worst, 1e-8);
        worst = 0;
        for (int i = 1; i <= p; i++) worst = Math.Max(worst, Math.Abs(se[i] - Math.Sqrt(inverse[i - 1, i - 1])));
        Check("standard errors are the square roots of its diagonal, at i(i+1)/2", worst, 1e-8);
        worst = 0;
        double total = 0;
        for (int i = 1; i <= records; i++)
        {
            double h = 0;
            for (int j = 0; j < p; j++) for (int k = 0; k < p; k++) h += d[i, j] * inverse[j, k] * d[i, k];
            h *= w[i];
            total += leverage[i];
            worst = Math.Max(worst, Math.Abs(leverage[i] - h));
        }
        Check("leverage h = w x'(X'WX)^-1 x, the diagonal of the hat matrix", worst, 1e-8);
        Console.WriteLine($"      sum of the leverages {total:F6} (the number of parameters is {p})");
        worst = 0;
        double devianceSum = 0;
        for (int i = 1; i <= records; i++) devianceSum += residual[i] * residual[i];
        Check("the squared deviance residuals add up to the deviance", Math.Abs(devianceSum - deviance), 1e-8);
        return leverage;
    }
}
