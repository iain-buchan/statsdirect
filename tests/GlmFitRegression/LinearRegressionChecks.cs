// Checks of the routines of the linear regression in StatsDirectUI/Builtins/Regress1.cs, by calculation: the means, covariances and correlations, the two
// singular value decompositions and what is made from them, the least squares fit by rotations a record at a time, the solving and inverting of its
// triangle, and the small routines for the Durbin-Watson statistic, the intervals about a prediction and the area under a polynomial.
using System.Reflection;
using StatsDirect.Builtins;

internal static partial class Program
{
    private static object[] CallWith(string name, params object[] args)
    {
        MethodInfo method = typeof(Regress1).GetMethod(name, Any) ?? throw new Exception("no method " + name);
        object result = method.Invoke(null, args);
        return args.Append(result).ToArray();   // the arguments as they were left, and then what was returned
    }

    private static void LinearRegressionChecks()
    {
        Console.WriteLine();
        Console.WriteLine("the routines of the linear regression");
        const int n = 12, p = 4;      // records, and columns of the design matrix: a constant and three predictors
        double[,] data = Data(n, 3, 5);
        double[,] x = new double[n + 1, p + 1];
        double[] y = new double[n + 1], w = new double[n + 1];
        for (int i = 1; i <= n; i++)
        {
            x[i, 1] = 1;
            for (int j = 1; j <= 3; j++) x[i, j + 1] = data[i, j];
            y[i] = 1.5 + 0.8 * x[i, 2] - 1.1 * x[i, 3] + 0.3 * x[i, 4] + 0.4 * Math.Sin(1.7 * i);
            w[i] = 1 + i % 3 * 0.5;
        }

        // the weighted least squares fit by the normal equations, for comparison
        (double[] beta, double[,] inverse, double rss) Normal(bool weighted, int columns)
        {
            double[,] xwx = new double[columns, columns]; double[] xwy = new double[columns];
            for (int j = 0; j < columns; j++)
                for (int i = 1; i <= n; i++)
                {
                    double wi = weighted ? w[i] : 1;
                    xwy[j] += wi * x[i, j + 1] * y[i];
                    for (int k = 0; k < columns; k++) xwx[j, k] += wi * x[i, j + 1] * x[i, k + 1];
                }
            double[,] inv = Inverse(xwx);
            double[] b = new double[columns];
            for (int j = 0; j < columns; j++) for (int k = 0; k < columns; k++) b[j] += inv[j, k] * xwy[k];
            double residual = 0;
            for (int i = 1; i <= n; i++)
            {
                double fitted = 0; for (int j = 0; j < columns; j++) fitted += x[i, j + 1] * b[j];
                residual += (weighted ? w[i] : 1) * (y[i] - fitted) * (y[i] - fitted);
            }
            return (b, inv, residual);
        }
        var weightedFit = Normal(true, p);
        var plainFit = Normal(false, p);
        double worst;

        // ---- means, standard deviations, covariance and correlation: the array is [variable, record]
        {
            double[,] byVariable = new double[3, n + 1];
            for (int i = 1; i <= n; i++) { byVariable[1, i] = x[i, 2]; byVariable[2, i] = y[i]; }
            double m1 = 0, m2 = 0; for (int i = 1; i <= n; i++) { m1 += x[i, 2] / n; m2 += y[i] / n; }
            double s11 = 0, s22 = 0, s12 = 0;
            for (int i = 1; i <= n; i++) { s11 += (x[i, 2] - m1) * (x[i, 2] - m1); s22 += (y[i] - m2) * (y[i] - m2); s12 += (x[i, 2] - m1) * (y[i] - m2); }
            Regress1.x_avsd(byVariable, n, 1, out double av, out double sd);
            Check("x_avsd: the mean, and the standard deviation with n - 1, of row id of an array that is [variable, record]", Math.Max(Math.Abs(av - m1), Math.Abs(sd - Math.Sqrt(s11 / (n - 1)))), 1e-13);
            Regress1.X_Comat(out double xc, out double xr, byVariable, n, 1, 2);
            Check("X_Comat: the covariance with n - 1, and the correlation, of two rows", Math.Max(Math.Abs(xc - s12 / (n - 1)), Math.Abs(xr - s12 / Math.Sqrt(s11 * s22))), 1e-13);
        }

        // ---- the two singular value decompositions
        foreach (string routine in new[] { "X_SVDCP", "SingularValueDecomposition" })
        {
            double[,] a = new double[n + 1, p + 1];
            for (int i = 1; i <= n; i++) for (int j = 1; j <= p; j++) a[i, j] = x[i, j];
            double[] s = new double[p + 1]; double[,] v = new double[p + 1, p + 1];
            if (routine == "X_SVDCP") { int fault = 0; Regress1.X_SVDCP(a, n, p, s, v, ref fault); Console.WriteLine("      X_SVDCP fault " + fault + ", singular values " + string.Join(", ", s.Skip(1).Select(t => t.ToString("F5")))); }
            else { object[] r = CallWith(routine, n, p, s, a, v); Console.WriteLine("      SingularValueDecomposition returns " + r[5] + ", singular values " + string.Join(", ", s.Skip(1).Select(t => t.ToString("F5")))); }
            double worstA = 0, worstU = 0, worstV = 0;
            for (int i = 1; i <= n; i++)
                for (int j = 1; j <= p; j++)
                {
                    double sum = 0; for (int k = 1; k <= p; k++) sum += a[i, k] * s[k] * v[j, k];
                    worstA = Math.Max(worstA, Math.Abs(sum - x[i, j]));
                }
            for (int j = 1; j <= p; j++)
                for (int k = 1; k <= p; k++)
                {
                    double uu = 0, vv = 0;
                    for (int i = 1; i <= n; i++) uu += a[i, j] * a[i, k];
                    for (int i = 1; i <= p; i++) vv += v[i, j] * v[i, k];
                    worstU = Math.Max(worstU, Math.Abs(uu - (j == k ? 1 : 0)));
                    worstV = Math.Max(worstV, Math.Abs(vv - (j == k ? 1 : 0)));
                }
            Check(routine + ": A = U S V', with U in place of A and V (not its transpose) in v", worstA, 1e-13);
            Check(routine + ": the columns of U and of V are orthonormal", Math.Max(worstU, worstV), 1e-13);
            Check(routine + ": no singular value is negative", s.Skip(1).All(t => t >= 0) ? 0 : 1, 0);
            if (routine == "X_SVDCP")
            {
                Regress1.X_Eigsrt(s, v, p);
                bool descending = true; for (int j = 2; j <= p; j++) if (s[j] > s[j - 1]) descending = false;
                worst = 0;
                for (int j = 1; j <= p; j++)
                    for (int k = 1; k <= p; k++)
                    {
                        double sum = 0, xtx = 0;
                        for (int l = 1; l <= p; l++) sum += v[j, l] * s[l] * s[l] * v[k, l];
                        for (int i = 1; i <= n; i++) xtx += x[i, j] * x[i, k];
                        worst = Math.Max(worst, Math.Abs(sum - xtx));
                    }
                Check("X_Eigsrt: the values in descending order, and the columns of V moved with them (V S^2 V' is still A'A)", descending ? worst : 1, 1e-12);
            }
        }

        // ---- the fit by singular value decomposition, and what is made from it
        {
            double[] sig = new double[n + 1];
            for (int i = 1; i <= n; i++) sig[i] = 1 / Math.Sqrt(w[i]);
            double[] b = new double[p + 1], s = new double[p + 1], fit = new double[n + 1], er = new double[n + 1];
            double[,] u = new double[n + 1, p + 1], v = new double[p + 1, p + 1];
            int returned = Regress1.X_SVGO(x, y, sig, n, p, b, u, v, s, fit, er);
            worst = 0; for (int j = 1; j <= p; j++) worst = Math.Max(worst, Math.Abs(b[j] - weightedFit.beta[j - 1]));
            Check("X_SVGO: the weighted least squares coefficients, the weights being 1 / sig^2 (returns " + returned + ")", worst, 1e-12);
            worst = 0;
            for (int i = 1; i <= n; i++)
            {
                double f = 0; for (int j = 1; j <= p; j++) f += x[i, j] * weightedFit.beta[j - 1];
                worst = Math.Max(worst, Math.Max(Math.Abs(fit[i] - f), Math.Abs(er[i] - (y[i] - f))));
            }
            Check("X_SVGO: the fitted values and the residuals y - fit, not weighted", worst, 1e-12);
            double[,] xtxi = new double[p + 1, p + 1]; double[] cn = new double[p + 1], hi = new double[n + 1];
            Regress1.X_SVDVRD(x, v, s, n, p, xtxi, cn, hi);
            worst = 0; for (int j = 1; j <= p; j++) for (int k = 1; k <= p; k++) worst = Math.Max(worst, Math.Abs(xtxi[j, k] - weightedFit.inverse[j - 1, k - 1]));
            Check("X_SVDVRD: xtxi = V S^-2 V', the inverse of X'WX", worst, 1e-12);
            worst = 0; double smax = s.Skip(1).Max();
            for (int j = 1; j <= p; j++) worst = Math.Max(worst, Math.Abs(cn[j] - smax / s[j]));
            Check("X_SVDVRD: cn is the largest singular value over each singular value", worst, 1e-12);
            worst = 0;
            for (int i = 1; i <= n; i++)
            {
                double h = 0; for (int j = 1; j <= p; j++) for (int k = 1; k <= p; k++) h += x[i, j] * weightedFit.inverse[j - 1, k - 1] * x[i, k];
                worst = Math.Max(worst, Math.Abs(hi[i] - h));
            }
            Check("X_SVDVRD: hi = x'(X'WX)^-1 x from the rows of x as given, so without the weight of the record", worst, 1e-12);
        }

        // ---- the least squares fit by rotations, a record at a time
        foreach (bool weighted in new[] { false, true })
        {
            var fit = weighted ? weightedFit : plainFit;
            int columns = 3 + (weighted ? 1 : 0) + 1;                     // the predictors, the weight if there is one, and y last
            double[,] xx = new double[n + 1, columns + 1];
            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= 3; j++) xx[i, j] = x[i, j + 1];
                if (weighted) xx[i, 4] = w[i];
                xx[i, columns] = y[i];
            }
            double[] b = new double[p + 1], d = new double[p + 1], xmin = new double[p + 1], xmax = new double[p + 1], wk = new double[2 * (p + 1) + 1];
            double[,] r = new double[p + 1, p + 1];
            int rank = 0, missing = 0, fault = 0; double dfe = 0, scpe = 0;
            Regress1.glsqr(0, 1, 0, n, columns, xx, -3, new int[2], -1, new int[2], 0, weighted ? 4 : 0, b, r, d, ref rank, ref dfe, ref scpe, ref missing, xmin, xmax, wk, ref fault);
            string kind = weighted ? "weighted" : "unweighted";
            Console.WriteLine($"      glsqr, {kind}: fault {fault}, rank {rank}, dfe {dfe}, records missed {missing}, d " + string.Join(", ", d.Skip(1).Select(t => t.ToString("G4"))));
            worst = 0; for (int j = 1; j <= p; j++) worst = Math.Max(worst, Math.Abs(b[j] - fit.beta[j - 1]));
            Check($"glsqr, {kind}: the least squares coefficients, the constant first", worst, 1e-12);
            Check($"glsqr, {kind}: scpe is the residual sum of squares and dfe the records less the rank", Math.Max(Math.Abs(scpe - fit.rss), Math.Abs(dfe - (n - p))), 1e-12);
            worst = 0; double below = 0;
            for (int j = 1; j <= p; j++)
                for (int k = 1; k <= p; k++)
                {
                    double sum = 0, xwx = 0;
                    for (int l = 1; l <= Math.Min(j, k); l++) sum += r[l, j] * r[l, k];
                    for (int i = 1; i <= n; i++) xwx += (weighted ? w[i] : 1) * x[i, j] * x[i, k];
                    worst = Math.Max(worst, Math.Abs(sum - xwx));
                    if (j > k) below = Math.Max(below, Math.Abs(r[j, k]));
                }
            Check($"glsqr, {kind}: R'R = X'WX, with R upper triangular in r and d set back to ones", Math.Max(worst, below), 1e-11);
            worst = 0;
            for (int j = 1; j <= p; j++)
            {
                double lo = double.MaxValue, hi = double.MinValue;
                for (int i = 1; i <= n; i++) { lo = Math.Min(lo, x[i, j]); hi = Math.Max(hi, x[i, j]); }
                worst = Math.Max(worst, Math.Max(Math.Abs(xmin[j] - lo), Math.Abs(xmax[j] - hi)));
            }
            Check($"glsqr, {kind}: xmin and xmax are the least and the greatest of each column, the constant included", worst, 0);
            double[,] covb = new double[p + 1, p + 1];
            Regress1.rcovarb(p, r, 2.5, covb, ref fault);
            worst = 0; for (int j = 1; j <= p; j++) for (int k = 1; k <= p; k++) worst = Math.Max(worst, Math.Abs(covb[j, k] - 2.5 * fit.inverse[j - 1, k - 1]));
            Check($"rcovarb, {kind}: s2 (R'R)^-1, the whole symmetric matrix", worst, 1e-12);

            // the triangle solved and inverted
            double[] c = new double[p + 1]; for (int j = 1; j <= p; j++) c[j] = j - 1.5;
            foreach (bool transpose in new[] { false, true })
            {
                double[] sol = (double[])c.Clone();
                fault = 0;
                Regress1.mxinv2(p, r, sol, true, transpose, false, r, out int rk, ref fault);
                worst = 0;
                for (int j = 1; j <= p; j++)
                {
                    double sum = 0;
                    for (int k = 1; k <= p; k++) sum += (transpose ? (k <= j ? r[k, j] : 0) : (k >= j ? r[j, k] : 0)) * sol[k];
                    worst = Math.Max(worst, Math.Abs(sum - c[j]));
                }
                Check($"mxinv2, {kind}: solves " + (transpose ? "R'x = b" : "R x = b") + " in place of b (rank " + rk + ")", worst, 1e-12);
            }
            double[,] rinv = new double[p + 1, p + 1];
            fault = 0;
            Regress1.mxinv2(p, r, null, false, false, true, rinv, out int _, ref fault);
            worst = 0;
            for (int j = 1; j <= p; j++)
                for (int k = 1; k <= p; k++)
                {
                    double sum = 0; for (int l = 1; l <= p; l++) sum += (l >= j ? r[j, l] : 0) * rinv[l, k];
                    worst = Math.Max(worst, Math.Abs(sum - (j == k ? 1 : 0)));
                    if (j > k) worst = Math.Max(worst, Math.Abs(rinv[j, k]));
                }
            Check($"mxinv2, {kind}: the inverse of R, upper triangular, in rinv", worst, 1e-12);
        }

        // ---- a predictor that is the sum of two others
        {
            double[,] xx = new double[n + 1, 6];
            for (int i = 1; i <= n; i++) { xx[i, 1] = x[i, 2]; xx[i, 2] = x[i, 3]; xx[i, 3] = x[i, 2] + x[i, 3]; xx[i, 4] = x[i, 4]; xx[i, 5] = y[i]; }
            const int q = 5;
            double[] b = new double[q + 1], d = new double[q + 1], xmin = new double[q + 1], xmax = new double[q + 1], wk = new double[2 * (q + 1) + 1];
            double[,] r = new double[q + 1, q + 1];
            int rank = 0, missing = 0, fault = 0; double dfe = 0, scpe = 0;
            Regress1.glsqr(0, 1, 0, n, 5, xx, -4, new int[2], -1, new int[2], 0, 0, b, r, d, ref rank, ref dfe, ref scpe, ref missing, xmin, xmax, wk, ref fault);
            Console.WriteLine($"      glsqr with the third predictor the sum of the first two: fault {fault}, rank {rank}, dfe {dfe}, coefficients " + string.Join(", ", b.Skip(1).Select(t => t.ToString("F6"))));
            Console.WriteLine("      the diagonal of R: " + string.Join(", ", Enumerable.Range(1, q).Select(j => r[j, j].ToString("F6"))));
            bool rowZero = true; for (int k = 1; k <= q; k++) if (r[4, k] != 0) rowZero = false;
            Check("glsqr: the predictor that adds nothing is dropped: rank 4, its coefficient zero and its row of R zero", rank == 4 && b[4] == 0 && rowZero ? 0 : 1, 0);
            Check("glsqr: the residual sum of squares and the degrees of freedom are those of the model without it", Math.Max(Math.Abs(scpe - plainFit.rss), Math.Abs(dfe - (n - 4))), 1e-11);
            double[,] covb = new double[q + 1, q + 1];
            Regress1.rcovarb(q, r, 1, covb, ref fault);
            worst = 0;
            int[] kept = { 1, 2, 3, 5 };
            for (int j = 0; j < 4; j++) for (int k = 0; k < 4; k++) worst = Math.Max(worst, Math.Abs(covb[kept[j], kept[k]] - plainFit.inverse[j, k]));
            for (int k = 1; k <= q; k++) worst = Math.Max(worst, Math.Max(Math.Abs(covb[4, k]), Math.Abs(covb[k, 4])));
            Console.WriteLine("      note: here the first two predictors stand for themselves, so the coefficients kept are those of the model without the third");
            Check("rcovarb: with a predictor dropped, the inverse for the others and zeros in its row and column (fault " + fault + ")", worst, 1e-11);
        }

        // ---- the mean taken out as the records come (isub 1): what it leaves
        {
            double[,] xx = new double[n + 1, 5];
            for (int i = 1; i <= n; i++) { for (int j = 1; j <= 3; j++) xx[i, j] = x[i, j + 1]; xx[i, 4] = y[i]; }
            double[] b = new double[p + 1], d = new double[p + 1], xmin = new double[p + 1], xmax = new double[p + 1], wk = new double[2 * (p + 1) + 1];
            double[,] r = new double[p + 1, p + 1];
            int rank = 0, missing = 0, fault = 0; double dfe = 0, scpe = 0;
            Regress1.glsqr(0, 1, 1, n, 4, xx, -3, new int[2], -1, new int[2], 0, 0, b, r, d, ref rank, ref dfe, ref scpe, ref missing, xmin, xmax, wk, ref fault);
            worst = 0; for (int j = 1; j <= p; j++) worst = Math.Max(worst, Math.Abs(b[j] - plainFit.beta[j - 1]));
            Console.WriteLine($"      glsqr with isub 1: fault {fault}, rank {rank}, dfe {dfe}, scpe {scpe:F8} (the residual sum of squares is {plainFit.rss:F8})");
            Console.WriteLine("      coefficients differ from the least squares ones by at most " + worst.ToString("E2"));
            double worstR = 0;
            for (int j = 1; j <= p; j++)
                for (int k = 1; k <= p; k++)
                {
                    double sum = 0, xtx = 0;
                    for (int l = 1; l <= Math.Min(j, k); l++) sum += r[l, j] * r[l, k];
                    for (int i = 1; i <= n; i++) xtx += x[i, j] * x[i, k];
                    worstR = Math.Max(worstR, Math.Abs(sum - xtx));
                }
            Console.WriteLine("      R'R differs from X'X by at most " + worstR.ToString("E2"));
        }

        // ---- the modified rotations
        {
            double d1 = 2.5, d2 = 0.7, x1 = 1.3; const double y1 = -0.9;
            double before = d1 * x1 * x1 + d2 * y1 * y1;
            double[] h = new double[6];
            object[] r = CallWith("drotmg", d1, d2, x1, y1, h);
            d1 = (double)r[0]; d2 = (double)r[1]; x1 = (double)r[2];
            Console.WriteLine("      drotmg: flag " + h[1] + ", h " + string.Join(", ", h.Skip(2).Select(t => t.ToString("F5"))) + $"; d1 {d1:F5}, d2 {d2:F5}, x {x1:F5}");
            Check("drotmg: d1 x^2 afterwards is d1 x^2 + d2 y^2 before: the length is kept, and y is rotated into x", Math.Abs(d1 * x1 * x1 - before), 1e-13);
        }

        // ---- small routines
        {
            object[] r = CallWith("Pythag", 3e200, 4e200);
            object[] r2 = CallWith("X_PYTHAG", 3e-200, 4e-200);
            Check("Pythag and X_PYTHAG: the square root of a^2 + b^2, where the squares themselves would overflow or underflow", Math.Max(Math.Abs((double)r[2] / 5e200 - 1), Math.Abs((double)r2[2] / 5e-200 - 1)), 1e-15);
            double[] er = new double[n + 1];
            for (int i = 1; i <= n; i++) er[i] = Math.Cos(2.3 * i) + 0.1 * i;
            double top = 0, bottom = 0;
            for (int i = 1; i <= n; i++) { bottom += er[i] * er[i]; if (i > 1) top += (er[i] - er[i - 1]) * (er[i] - er[i - 1]); }
            Regress1.x_dwsd(er, n, out double dw);
            Check("x_dwsd: the Durbin-Watson statistic, the squares of the successive differences over the squares of the residuals", Math.Abs(dw - top / bottom), 1e-14);
            double[] xv = { 0, 1, 0.4, -0.7, 1.2 };
            double[,] xtxi1 = new double[p + 1, p + 1];
            for (int j = 1; j <= p; j++) for (int k = 1; k <= p; k++) xtxi1[j, k] = plainFit.inverse[j - 1, k - 1];
            double quad = 0; for (int j = 1; j <= p; j++) for (int k = 1; k <= p; k++) quad += xv[j] * xtxi1[j, k] * xv[k];
            Regress1.x_ciyp(xv, xtxi1, p, 0.37, 2.2, out double cl, out double pl);
            Check("x_ciyp: cit sqrt(rms x'(X'X)^-1 x) for the mean, and cit sqrt(rms (1 + x'(X'X)^-1 x)) for a new record", Math.Max(Math.Abs(cl - 2.2 * Math.Sqrt(0.37 * quad)), Math.Abs(pl - 2.2 * Math.Sqrt(0.37 * (1 + quad)))), 1e-14);
        }

        // ---- the area under a polynomial
        {
            double[] bd = { 0, 1.5, -0.8, 0.25, 0.1 };          // 1.5 - 0.8 x + 0.25 x^2 + 0.1 x^3
            double F(double t) => 1.5 - 0.8 * t + 0.25 * t * t + 0.1 * t * t * t;
            Check("polyfunc: bd[1] + bd[2] x + ... + bd[ip] x^(ip - 1)", Math.Max(Math.Abs(Regress1.polyfunc(1.7, bd, 4) - F(1.7)), Math.Abs(Regress1.polyfunc(0, bd, 4) - F(0))), 1e-15);
            const double a = -1, b = 2.5;
            double s = 0; worst = 0;
            for (int stage = 1; stage <= 6; stage++)
            {
                s = Regress1.trapzd(a, b, s, stage, bd, 4);
                int panels = 1 << (stage - 1);
                double h = (b - a) / panels, sum = 0.5 * (F(a) + F(b));
                for (int k = 1; k < panels; k++) sum += F(a + k * h);
                worst = Math.Max(worst, Math.Abs(s - h * sum));
            }
            Check("trapzd: stage n, given the result of stage n - 1, is the trapezoidal rule with 2^(n - 1) panels", worst, 1e-14);

            // interpolation through five points of a cubic, the points starting at the first and at the third element
            double[] xa = new double[9], ya = new double[9];
            for (int i = 1; i <= 8; i++) { xa[i] = 0.3 * i - 1; ya[i] = F(xa[i]); }
            foreach (int start in new[] { 1, 3 })
            {
                double dy = 0; int fault = 0;
                Regress1.polint(xa, ya, start, 5, 0.17, out double yy, ref dy, ref fault);
                Console.WriteLine($"      polint from element {start}: {yy:F12}, where the cubic is {F(0.17):F12} (fault {fault})");
                Check("polint: the polynomial through the n points that start at element " + start + ", at x", Math.Abs(yy - F(0.17)), 1e-12);
            }

            // the area as the program finds it (the loop of x_qromb in Regress.cs), for each degree that polynomial regression offers
            for (int degree = 2; degree <= 9; degree++)
            {
                int ip = degree + 1;
                double[] c = new double[ip + 1];
                for (int j = 1; j <= ip; j++) c[j] = Math.Cos(1.3 * j + degree) * Math.Pow(0.6, j - 1);
                const double lower = -2.2, upper = 3.4;
                double exact = 0, scale = 0;
                for (int j = 1; j <= ip; j++)
                {
                    exact += c[j] * (Math.Pow(upper, j) - Math.Pow(lower, j)) / j;
                    scale += Math.Abs(c[j]) * (Math.Pow(upper, j) + Math.Abs(Math.Pow(lower, j))) / j;
                }
                const int jmax = 16, k = 5;
                double[] h = new double[jmax + 2], st = new double[jmax + 2];
                h[1] = 1;
                double ss = 0, ds = 0; int stages = 0; bool converged = false;
                for (int j = 1; j <= jmax && !converged; j++)
                {
                    st[j] = Regress1.trapzd(lower, upper, st[j], j, c, ip);
                    stages = j;
                    if (j >= k)
                    {
                        int fault = 0;
                        Regress1.polint(h, st, j - (k - 1), k, 0.0, out ss, ref ds, ref fault);
                        if (fault == 0 && Math.Abs(ds) <= 100 * StatsDirect.Numerics.Constant.EPSNEG * Math.Abs(ss)) converged = true;
                    }
                    st[j + 1] = st[j];
                    h[j + 1] = 0.25 * h[j];
                }
                Check("the area under a polynomial of degree " + degree + " (" + stages + " stages" + (converged ? "" : ", not converged") + ")", converged ? Math.Abs(ss - exact) / scale : 1, 1e-13);
            }
        }

        // ---- the search for a missing value, and the check of a record
        {
            double[] v = { 0, 1, 2, 3, StatsDirect.Numerics.Constant.MISSING, 5 };
            object[] r1 = CallWith("IndexNaN", 5, v, 1);
            object[] r2 = CallWith("IndexNaN", 3, v, 1);
            object[] r3 = CallWith("IndexNaN", 2, v, 4);
            Console.WriteLine($"      IndexNaN with the missing value at 4: searching 1 to 5 gives {r1[3]}; n = 3 from 1 gives {r2[3]}; n = 2 from 4 gives {r3[3]}");
            Check("IndexNaN: n is the last element searched, not the number of elements", (int)r1[3] == 4 && (int)r2[3] == 0 && (int)r3[3] == 0 ? 0 : 1, 0);
        }

        // ---- what is done with a record whose frequency or weight is missing, or negative
        {
            double missing = StatsDirect.Numerics.Constant.MISSING;
            (int igo, int fault, int nmiss, double frq, double wt) Look(double frequency, double weight)
            {
                double[,] record = new double[2, 3];
                record[1, 1] = frequency;
                record[1, 2] = weight;
                object[] r = CallWith("CheckObs", 0, record, 1, 1, 1, 2, missing, 0, 0.0, 0.0, 0, 0);
                return ((int)r[10], (int)r[11], (int)r[7], (double)r[8], (double)r[9]);
            }
            var good = Look(2, 1.5); var none = Look(0, 1.5); var lostF = Look(missing, 1.5); var lostW = Look(2, missing); var badF = Look(-2, 1.5); var badW = Look(2, -1.5);
            Check("CheckObs: a record with a frequency and a weight is used (igo 0)", good.igo == 0 && good.fault == 0 && good.frq == 2 && good.wt == 1.5 ? 0 : 1, 0);
            Check("CheckObs: a frequency of zero passes the record over (igo 1)", none.igo == 1 && none.fault == 0 ? 0 : 1, 0);
            Check("CheckObs: a missing frequency or weight passes the record over and counts it (igo 2)",
                lostF.igo == 2 && lostF.fault == 0 && lostF.nmiss == 1 && lostW.igo == 2 && lostW.fault == 0 && lostW.nmiss == 1 ? 0 : 1, 0);
            Check("CheckObs: a negative frequency or weight stops the fit (igo 3; faults 3 and 6 when the fit is made in one call)",
                badF.igo == 3 && badF.fault == 3 && badW.igo == 3 && badW.fault == 6 ? 0 : 1, 0);
        }

        // ---- the fit passes over a record with a missing value, wherever in the record it is, and stops at a negative weight
        {
            double missing = StatsDirect.Numerics.Constant.MISSING;
            const int lost = 5;
            // the weighted fit of the other records, by the normal equations
            double[,] xwx = new double[p, p]; double[] xwy = new double[p];
            for (int i = 1; i <= n; i++)
                if (i != lost)
                    for (int j = 0; j < p; j++)
                    {
                        xwy[j] += w[i] * x[i, j + 1] * y[i];
                        for (int k = 0; k < p; k++) xwx[j, k] += w[i] * x[i, j + 1] * x[i, k + 1];
                    }
            double[,] inv = Inverse(xwx);
            double[] expected = new double[p];
            for (int j = 0; j < p; j++) for (int k = 0; k < p; k++) expected[j] += inv[j, k] * xwy[k];
            double expectedRss = 0;
            for (int i = 1; i <= n; i++)
                if (i != lost)
                {
                    double fitted = 0; for (int j = 0; j < p; j++) fitted += x[i, j + 1] * expected[j];
                    expectedRss += w[i] * (y[i] - fitted) * (y[i] - fitted);
                }
            foreach (string where in new[] { "outcome", "last predictor", "weight", "negative weight" })
            {
                double[,] xx = new double[n + 1, 6];
                for (int i = 1; i <= n; i++) { for (int j = 1; j <= 3; j++) xx[i, j] = x[i, j + 1]; xx[i, 4] = w[i]; xx[i, 5] = y[i]; }
                if (where == "outcome") xx[lost, 5] = missing;
                else if (where == "last predictor") xx[lost, 3] = missing;
                else if (where == "weight") xx[lost, 4] = missing;
                else xx[lost, 4] = -1;
                double[] b = new double[p + 1], d = new double[p + 1], xmin = new double[p + 1], xmax = new double[p + 1], wk = new double[2 * (p + 1) + 1];
                double[,] r = new double[p + 1, p + 1];
                int rank = 0, passedOver = 0, fault = 0; double dfe = 0, scpe = 0;
                Regress1.glsqr(0, 1, 0, n, 5, xx, -3, new int[2], -1, new int[2], 0, 4, b, r, d, ref rank, ref dfe, ref scpe, ref passedOver, xmin, xmax, wk, ref fault);
                if (where == "negative weight")
                {
                    Check("glsqr: a negative weight stops the fit, with fault 6", fault == 6 ? 0 : 1, 0);
                    continue;
                }
                worst = 0; for (int j = 1; j <= p; j++) worst = Math.Max(worst, Math.Abs(b[j] - expected[j - 1]));
                worst = Math.Max(worst, Math.Max(Math.Abs(scpe - expectedRss), Math.Abs(dfe - (n - 1 - p))));
                Check($"glsqr: a record whose {where} is missing is passed over and counted: the fit is that of the other records", fault == 0 && passedOver == 1 ? worst : 1, 1e-12);
            }
        }
    }
}
