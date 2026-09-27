using System;
using System.Globalization;
using StatsDirect.Numerics;

namespace StatsDirect.Builtins
{
    public class Regress1
    {
        /// <summary>
        /// The covariance xc and the correlation xr of two variables.
        /// </summary>
        /// <remarks>
        /// x is x[variable, record], not the other way about: idx and idy are the two variables, and nx is the number of records.  The covariance is
        /// the sum of the products of the deviations from the means over nx - 1.
        /// </remarks>
        public static void X_Comat(out double xc, out double xr, double[,] x, int nx, int idx, int idy)
        {
            x_avsd(x, nx, idx, out double avx, out double sdx);
            x_avsd(x, nx, idy, out double avy, out double sdy);
            double co = 0;
            for (int i = 1; i <= nx; i++)
                co += (x[idx, i] - avx) * (x[idy, i] - avy);
            xr = co / ((nx - 1.0) * sdx * sdy);
            xc = co / (nx - 1.0);
        }

        /// <summary>
        /// The mean av and the standard deviation sd, with nx - 1, of variable id of x, which is x[variable, record].
        /// </summary>
        /// <remarks>
        /// The sum of squares is taken about the mean in a second pass.  ep is the sum of the deviations themselves, which would be zero but for
        /// rounding: taking ep^2 / nx from the sum of squares removes most of the error that rounding of the mean has put in.
        /// </remarks>
        public static void x_avsd(double[,] x, int nx, int id, out double av, out double sd)
        {
            double sum = 0.0;
            for (int j = 1; j <= nx; j++)
                sum += x[id, j];
            av = sum / nx;
            double ep = 0;
            double var = 0;
            for (int j = 1; j <= nx; j++)
            {
                double s = x[id, j] - av;
                double p = s * s;
                ep += s;
                var += p;
            }
            var = (var - ep * ep / nx) / (nx - 1.0);
            sd = Math.Sqrt(var);
        }


        /// <summary>
        /// The singular value decomposition A = U S V' of a matrix of nx rows and p columns, nx no less than p.
        /// </summary>
        /// <remarks>
        /// On entry ad holds A, as ad[row, column].  On return ad holds U, whose p columns are orthonormal, wd the singular values, which are not
        /// negative and come in no particular order, and vd the matrix V itself, not its transpose.  ifault is set to 1 if nx is less than p, and to 2
        /// if a singular value has not been found in 30 steps; otherwise it is left as it was.
        /// The method is that of SingularValueDecomposition, further down: reduction to bidiagonal form by Householder reflections, and then the QR
        /// algorithm.  LAPACK's DGESVD does the same job, and returns V' and the singular values in descending order.
        /// </remarks>
        public static void X_SVDCP(double[,] ad, int nx, int p, double[] wd, double[,] vd, ref int ifault)
        {
            int i; int j; int k; int l = 0;
            double s; double f; double h;

            double[] rv1 = new double[p + 1];
            if (nx < p)
            {
                ifault = 1;
                return;
            }
            // Reduction to bidiagonal form.  For each i in turn a reflection clears column i below the diagonal, and another clears row i beyond the
            // element next to the diagonal.  The vectors of the reflections are left in ad where the zeros would be; wd takes the diagonal and rv1 the
            // elements above it.  Each column and row is scaled by the sum of its absolute values (sca) first, to keep its squares within range.
            // anorm becomes the size of the bidiagonal matrix: the largest sum of a diagonal element and the element above it.
            const int maxit = 30;
            double g = 0.0;
            double sca = 0.0;
            double anorm = 0.0;
            for (i = 1; i <= p; i++)
            {
                l = i + 1;
                rv1[i] = sca * g;
                g = 0.0;
                s = 0.0;
                sca = 0.0;
                if (i <= nx)
                {
                    for (k = i; k <= nx; k++)
                        sca += Math.Abs(ad[k, i]);
                    if (sca != 0.0)
                    {
                        for (k = i; k <= nx; k++)
                        {
                            ad[k, i] = ad[k, i] / sca;
                            s += ad[k, i] * ad[k, i];
                        }
                        f = ad[i, i];
                        g = f >= 0.0 ? -Math.Abs(Math.Sqrt(s)) : Math.Abs(Math.Sqrt(s));
                        h = f * g - s;
                        ad[i, i] = f - g;
                        for (j = l; j <= p; j++)
                        {
                            s = 0.0;
                            for (k = i; k <= nx; k++)
                                s += ad[k, i] * ad[k, j];
                            f = s / h;
                            for (k = i; k <= nx; k++)
                                ad[k, j] = ad[k, j] + f * ad[k, i];
                        }
                        for (k = i; k <= nx; k++)
                            ad[k, i] = sca * ad[k, i];
                    }
                }
                wd[i] = sca * g;
                g = 0.0;
                s = 0.0;
                sca = 0.0;
                if (i <= nx & i != p)
                {
                    for (k = l; k <= p; k++)
                        sca += Math.Abs(ad[i, k]);
                    if (sca != 0.0)
                    {
                        for (k = l; k <= p; k++)
                        {
                            ad[i, k] = ad[i, k] / sca;
                            s += ad[i, k] * ad[i, k];
                        }
                        f = ad[i, l];
                        g = f >= 0.0 ? -Math.Abs(Math.Sqrt(s)) : Math.Abs(Math.Sqrt(s));
                        h = f * g - s;
                        ad[i, l] = f - g;
                        for (k = l; k <= p; k++)
                            rv1[k] = ad[i, k] / h;
                        for (j = l; j <= nx; j++)
                        {
                            s = 0.0;
                            for (k = l; k <= p; k++)
                                s += ad[j, k] * ad[i, k];
                            for (k = l; k <= p; k++)
                                ad[j, k] = ad[j, k] + s * rv1[k];
                        }
                        for (k = l; k <= p; k++)
                            ad[i, k] = sca * ad[i, k];
                    }
                }
                if (Math.Abs(wd[i]) + Math.Abs(rv1[i]) > anorm)
                    anorm = Math.Abs(wd[i]) + Math.Abs(rv1[i]);
            }

            // The reflections that were made from the right are multiplied out into vd, from the last to the first: V
            for (i = p; i >= 1; i--)
            {
                if (i < p)
                {
                    if (g != 0.0)
                    {
                        for (j = l; j <= p; j++)
                            vd[j, i] = ad[i, j] / ad[i, l] / g;
                        for (j = l; j <= p; j++)
                        {
                            s = 0.0;
                            for (k = l; k <= p; k++)
                                s += ad[i, k] * vd[k, j];
                            for (k = l; k <= p; k++)
                                vd[k, j] = vd[k, j] + s * vd[k, i];
                        }
                    }
                    for (j = l; j <= p; j++)
                    {
                        vd[i, j] = 0.0;
                        vd[j, i] = 0.0;
                    }
                }
                vd[i, i] = 1.0;
                g = rv1[i];
                l = i;
            }
            // Those that were made from the left are multiplied out in ad itself, from the last to the first: U
            for (i = p; i >= 1; i--)
            {
                l = i + 1;
                g = wd[i];
                for (j = l; j <= p; j++)
                {
                    ad[i, j] = 0.0;
                }
                if (g != 0.0)
                {
                    g = 1.0 / g;
                    for (j = l; j <= p; j++)
                    {
                        s = 0.0;
                        for (k = l; k <= nx; k++)
                            s += ad[k, i] * ad[k, j];
                        f = s / ad[i, i] * g;
                        for (k = i; k <= nx; k++)
                            ad[k, j] = ad[k, j] + f * ad[k, i];
                    }
                    for (j = i; j <= nx; j++)
                        ad[j, i] = ad[j, i] * g;
                }
                else
                {
                    for (j = i; j <= nx; j++)
                        ad[j, i] = 0.0;
                }
                ad[i, i] = ad[i, i] + 1.0;
            }
            // The bidiagonal matrix is made diagonal, from the last singular value up, by QR steps with shifts: at most maxit for each value
            for (k = p; k >= 1; k--)
            {
                int its;
                for (its = 1; its <= maxit; its++)
                {
                    int nm;
                    double z;
                    double c;
                    double y;
                    // The test for splitting, working up from row k.  If an element above the diagonal (rv1) is negligible the matrix splits there.  If
                    // a diagonal element (wd) is negligible, the element beside it is rotated away first, the rotations going into U.  An element is
                    // negligible if adding it to anorm leaves anorm as it was.
                    for (l = k; l >= 1; l--)
                    {
                        nm = l - 1;
                        if (Math.Abs(rv1[l]) + anorm == anorm)
                        {
                            break;
                        }
                        if (Math.Abs(wd[nm]) + anorm == anorm)
                        {
                            c = 0.0;
                            s = 1.0;
                            for (i = l; i <= k; i++)
                            {
                                f = s * rv1[i];
                                rv1[i] = c * rv1[i];
                                if (Math.Abs(f) + anorm == anorm)
                                    break;
                                g = wd[i];
                                h = X_PYTHAG(f, g);
                                wd[i] = h;
                                h = 1.0 / h;
                                c = g * h;
                                s = -(f * h);
                                for (j = 1; j <= nx; j++)
                                {
                                    y = ad[j, nm];
                                    z = ad[j, i];
                                    ad[j, nm] = y * c + z * s;
                                    ad[j, i] = -(y * s) + z * c;
                                }
                            }
                            break;
                        }
                    }
                    // Converged, if the split is at row k itself: the singular value is made positive, its sign going to its column of V
                    z = wd[k];
                    if (l == k)
                    {
                        if (z < 0.0)
                        {
                            wd[k] = -z;
                            for (j = 1; j <= p; j++)
                                vd[j, k] = -vd[j, k];
                        }
                        break;
                    }
                    if (its >= maxit)
                    {
                        ifault = 2;
                        return;
                    }
                    // The shift, from the 2 by 2 block at the bottom
                    double x = wd[l];
                    nm = k - 1;
                    y = wd[nm];
                    g = rv1[nm];
                    h = rv1[k];
                    f = ((y - z) * (y + z) + (g - h) * (g + h)) / (2.0 * h * y);
                    g = X_PYTHAG(f, 1.0);
                    var temp = f >= 0 ? Math.Abs(g) : -Math.Abs(g);
                    f = ((x - z) * (x + z) + h * (y / (f + temp) - h)) / x;
                    // The QR step, on rows l to k: rotations from the right, which go into V, in turn with rotations from the left, which go into U
                    c = 1.0;
                    s = 1.0;
                    for (j = l; j <= nm; j++)
                    {
                        i = j + 1;
                        g = rv1[i];
                        y = wd[i];
                        h = s * g;
                        g = c * g;
                        z = X_PYTHAG(f, h);
                        rv1[j] = z;
                        c = f / z;
                        s = h / z;
                        f = x * c + g * s;
                        g = -(x * s) + g * c;
                        h = y * s;
                        y *= c;
                        int jj;
                        for (jj = 1; jj <= p; jj++)
                        {
                            x = vd[jj, j];
                            z = vd[jj, i];
                            vd[jj, j] = x * c + z * s;
                            vd[jj, i] = -(x * s) + z * c;
                        }
                        z = X_PYTHAG(f, h);
                        wd[j] = z;
                        if (z != 0.0)
                        {
                            z = 1.0 / z;
                            c = f * z;
                            s = h * z;
                        }
                        f = c * g + s * y;
                        x = -(s * g) + c * y;
                        for (jj = 1; jj <= nx; jj++)
                        {
                            y = ad[jj, j];
                            z = ad[jj, i];
                            ad[jj, j] = y * c + z * s;
                            ad[jj, i] = -(y * s) + z * c;
                        }
                    }
                    rv1[l] = 0.0;
                    rv1[k] = f;
                    wd[k] = x;
                }
            }
        }

        /// <summary>
        /// The square root of a^2 + b^2, found without squaring a or b, so that it neither overflows nor underflows.
        /// </summary>
        private static double X_PYTHAG(double a, double b)
        {
            double absa = Math.Abs(a);
            double absb = Math.Abs(b);
            if (absa > absb)
                return absa * Math.Sqrt(1.0 + absb / absa * (absb / absa));
            if (absb == 0.0)
                return 0.0;
            return absb * Math.Sqrt(1.0 + absa / absb * (absa / absb));
        }

        /// <summary>
        /// Puts the n values in d into descending order, and moves the columns of v with them.
        /// </summary>
        /// <remarks>
        /// By selection: for each place in turn the largest of the values from there on is found and exchanged into it, with its column.  Used on the
        /// singular values and V of a decomposition; U is not given, and so is not reordered.
        /// </remarks>
        public static void X_Eigsrt(double[] d, double[,] v, int n)
        {
            for (int i = 1; i < n; i++)
            {
                int k = i;
                double p = d[i];
                int j;
                for (j = i + 1; j <= n; j++)
                {
                    if (d[j] >= p)
                    {
                        k = j;
                        p = d[j];
                    }
                }
                if (k != i)
                {
                    d[k] = d[i];
                    d[i] = p;
                    for (j = 1; j <= n; j++)
                    {
                        p = v[j, i];
                        v[j, i] = v[j, k];
                        v[j, k] = p;
                    }
                }
            }
        }

        /// <summary>
        /// The least squares coefficients from a singular value decomposition: b = V S^-1 U'(y / sig).
        /// </summary>
        /// <remarks>
        /// ud, wd and vd hold U, the singular values and V of the matrix whose rows are those of X divided by sig.  t holds S^-1 U'(y / sig).  A
        /// singular value of zero is left out of the sum, which gives the shortest of the solutions when X is not of full rank.
        /// </remarks>
        private static void X_SVDBKD(double[,] ud, double[] wd, double[,] vd, int nx, int p, double[] yd, double[] sig, double[] bd)
        {
            double[] t = new double[p + 1];
            for (int j = 1; j <= p; j++)
            {
                double s = 0.0;
                if (wd[j] != 0.0)
                {
                    for (int i = 1; i <= nx; i++)
                        s += ud[i, j] * yd[i] / sig[i];
                    s /= wd[j];
                }
                t[j] = s;
            }
            for (int j = 1; j <= p; j++)
            {
                double s = 0.0;
                for (int k = 1; k <= p; k++)
                    s += vd[j, k] * t[k];
                bd[j] = s;
            }
        }

        /// <summary>
        /// From a singular value decomposition: the inverse of X'X, a condition number for each singular value, and the leverages.
        /// </summary>
        /// <remarks>
        /// v and w hold V and the singular values.  xtxi = V S^-2 V', which is the inverse of X'X for the matrix that was decomposed; a singular value
        /// of zero is left out.  cn[i] is the largest singular value over singular value i, or zero for a singular value of zero.  hi[j] is
        /// x'(xtxi) x for row j of the x that is given here.  X_SVGO decomposes the matrix with its rows weighted, and the caller gives this routine
        /// the rows without their weights: hi is then x'(X'WX)^-1 x, the leverage without the weight of the record.
        /// </remarks>
        public static void X_SVDVRD(double[,] x, double[,] v, double[] w, int nx, int p, double[,] xtxi, double[] cn, double[] hi)
        {
            double[] owt = new double[p + 1];
            double wmax = w[1];
            for (int i = 1; i <= p; i++)
            {
                owt[i] = 0.0;
                if (w[i] != 0.0)
                    owt[i] = 1.0 / (w[i] * w[i]);
                if (w[i] > wmax)
                    wmax = w[i];
            }
            for (int i = 1; i <= p; i++)
                cn[i] = wmax * w[i] * owt[i];
            for (int j = 1; j <= p; j++)
            {
                for (int i = j; i <= p; i++)
                {
                    double sum = 0.0;
                    for (int k = 1; k <= p; k++)
                        sum += v[j, k] * v[i, k] * owt[k];
                    xtxi[j, i] = sum;
                    xtxi[i, j] = sum;
                }
            }
            for (int j = 1; j <= nx; j++)
            {
                hi[j] = 0.0;
                for (int i = 1; i <= p; i++)
                {
                    double sum = 0.0;
                    for (int k = 1; k <= p; k++)
                        sum += xtxi[i, k] * x[j, k];
                    hi[j] += x[j, i] * sum;
                }
            }
        }

        /// <summary>
        /// The least squares fit by singular value decomposition, which still gives an answer when the design matrix is not of full rank.
        /// </summary>
        /// <param name="xd">The design matrix, xd[record, column].</param>
        /// <param name="yd">The response.</param>
        /// <param name="sig">For each record, the standard deviation of its error: the weight of the record is 1 / sig^2.</param>
        /// <param name="nx">The number of records.</param>
        /// <param name="p">The number of columns.</param>
        /// <param name="bd">On return, the coefficients.</param>
        /// <param name="ud">On return, U of the decomposition of the matrix whose rows are those of xd divided by sig.</param>
        /// <param name="vd">On return, V of that decomposition.</param>
        /// <param name="wd">On return, its singular values; one that is less than 1e-12 of the largest is set to zero, and left out of the fit.</param>
        /// <param name="yfit">On return, the fitted values.</param>
        /// <param name="er">On return, the residuals y - yfit, not weighted.</param>
        /// <returns>0 on successful decomposition, non-0 if no decomposition found</returns>
        public static int X_SVGO(double[,] xd, double[] yd, double[] sig, int nx, int p, double[] bd, double[,] ud, double[,] vd, double[] wd, double[] yfit, double[] er)
        {
            for (int i = 1; i <= nx; i++)
            {
                double osig = 1.0 / sig[i];
                for (int j = 1; j <= p; j++)
                    ud[i, j] = xd[i, j] * osig;
            }
            int ifault = SingularValueDecomposition(nx, p, wd, ud, vd);
            double wmax = wd[1];
            for (int j = 1; j <= p; j++)
                if (wmax < wd[j])
                    wmax = wd[j];
            double tol = wmax * 1.0e-12;
            for (int j = 1; j <= p; j++)
                if (wd[j] < tol)
                    wd[j] = 0.0;
            X_SVDBKD(ud, wd, vd, nx, p, yd, sig, bd);
            for (int i = 1; i <= nx; i++)
            {
                double sum = 0.0;
                for (int j = 1; j <= p; j++)
                    sum += bd[j] * xd[i, j];
                yfit[i] = sum;
                er[i] = yd[i] - yfit[i];
            }
            return ifault;
        }

        /// <summary>
        /// The least squares fit of a linear model, made a record at a time by rotations, without the matrix X'X being formed.
        /// </summary>
        /// <remarks>
        /// The triangle R of X = QR is built up by taking each record in turn and rotating it into the triangle found so far, so that the records need
        /// never all be held at once and may be given in several calls.  The rotations are the modified ones (drotmg), which need no square roots:
        /// while the fit is being made the triangle is held as r with a scale factor d for each row, row i standing for sqrt(d[i]) times what r holds.
        ///
        ///     ido       0 for the whole fit in one call; 1 for the first of several calls, 2 for a call in the middle and 3 for the last
        ///     intcep    1 if a constant is fitted, which then comes first among the coefficients; 0 if not
        ///     isub      1 to have the means taken out as the records come (the first row then holds the sum of the weights and the weighted sums)
        ///     nrow      the number of records in x; negative, to take those records out of a fit again
        ///     nvar      the number of columns of x, which is x[record, column]
        ///     iind      the number of predictors, whose columns are in indind; negative, for the first -iind columns
        ///     idep      the number of dependent variables, whose columns are in inddep; negative, for the last -idep columns
        ///     ifrq      the column of frequencies, or 0;  iwt  the column of weights, or 0
        ///     b         on return, the coefficients for the first dependent variable
        ///     r, d      on return, R in the upper triangle of r, with a diagonal that is not negative, and d set to ones: R'R = X'WX
        ///     irank     on return, the rank: the coefficients less those of the predictors that were dropped
        ///     dfe       on return, the degrees of freedom for error: the sum of the frequencies less the rank
        ///     scpe      on return, the residual sum of squares of the first dependent variable
        ///     nrmiss    on return, the number of records passed over for a missing value
        ///     xmin, xmax  on return, the least and the greatest of each column of the design matrix, the constant included
        ///     wk        work space: the record, as the constant, the predictors and then the dependent variables
        ///     ifault    on entry not zero, nothing is done; on return 10 or 11 if a record taken out held the least or the greatest of a column,
        ///               12 if there are no degrees of freedom left for error, and the faults of CheckObs and mxinv2
        ///
        /// A predictor that adds nothing to those before it is dropped: its coefficient and its row of R are zero.  Only the first dependent
        /// variable is carried through to b and scpe.  The callers give the whole fit in one call, with ido 0 and isub 0.
        /// </remarks>
        public static void glsqr(int ido, int intcep, int isub, int nrow, int nvar, double[,] x, int iind, int[] indind, int idep, int[] inddep, int ifrq, int iwt, double[] b, double[,] r, double[] d, ref int irank, ref double dfe, ref double scpe, ref int nrmiss, double[] xmin, double[] xmax, double[] wk, ref int ifault)
        {
            double[] sparam = new double[5 + 1];
            double frq = 0, wt = 0;

            bool skip = false;

            if (ifault != 0)
            {
                irank = 0;
                return;
            }

            // The coefficients are the constant, if there is one, and then the predictors; in wk the dependent variables follow them, from idepx
            int ndep = Math.Abs(idep);
            int nind = Math.Abs(iind);
            int ncoef = intcep + nind;
            int intp1 = intcep + 1;
            int idepx = ncoef + 1;

            double[,] b2 = new double[ncoef + 1, ncoef + 1];

            // tolerance of 100 * largest relative spacing
            const double tol = 100.0 * Constant.EPSILON;
            const double tolsq = tol * tol;

            // A fresh start: nothing in R, and the scale factors at one
            if (ido <= 1)
            {
                nrmiss = 0;
                dfe = 0.0;
                for (int i = 1; i <= ncoef; i++)
                    for (int j = 1; j <= ncoef; j++)
                        r[j, i] = 0.0;
                for (int i = 1; i <= ndep; i++)
                    for (int j = 1; j <= ncoef; j++)
                        b2[j, i] = 0.0;
                for (int j = 1; j <= ncoef; j++)
                {
                    d[j] = 1.0;
                    xmin[j] = Constant.MISSING;
                    xmax[j] = Constant.MISSING;
                }
                scpe = 0.0;
            }

            // A negative nrow takes records out of the fit: irow is then -1, and the frequency of each record is made negative
            int nobs;
            int irow;
            if (nrow < 0)
            {
                nobs = -nrow;
                irow = -1;
            }
            else
            {
                nobs = nrow;
                irow = 1;
            }
            int i1 = isub == 0 ? 1 : 2;

            // A record at a time: the record is put in wk and looked at for missing values, its range is noted, and it is rotated into the triangle.
            // A record with a missing value is passed over, and counted in nrmiss.
            for (int iobs = 1; iobs <= nobs; iobs++)
            {
                CheckObs(ido, x, iobs, irow, ifrq, iwt, Constant.MISSING, ref nrmiss, ref frq, ref wt, out int igo, ref ifault);
                if (igo == 3)
                    return;
                if (igo != 2 && igo != 1)
                {
                    if (intcep == 1)
                        wk[1] = 1.0;
                    for (int i = 1; i <= iind; i++)
                        wk[intcep + i] = x[iobs, indind[i]];
                    for (int i = 1; i <= -iind; i++)
                        wk[intcep + i] = x[iobs, i];
                    // the predictors are elements intp1 to intcep + nind of wk (the search had stopped at element nind)
                    if (IndexNaN(intcep + nind, wk, intp1) > 0)
                    {
                        nrmiss += irow;
                    }
                    else
                    {
                        int jdepx = idepx;
                        for (int i = 1; i <= idep; i++)
                        {
                            wk[jdepx] = x[iobs, inddep[i]];
                            jdepx++;
                        }
                        for (int i = idep + 1; i <= 0; i++)
                        {
                            wk[jdepx] = x[iobs, nvar + i];
                            jdepx++;
                        }
                        // the dependent variables are elements idepx to idepx + ndep - 1 (the search had stopped at element ndep, before it began)
                        if (IndexNaN(idepx + ndep - 1, wk, idepx) > 0)
                        {
                            nrmiss += irow;
                        }
                        else
                        {
                            dfe += frq;
                            if (irow == 1)
                            {
                                if (ncoef > 0)
                                {
                                    if (xmin[1] == Constant.MISSING)
                                    {
                                        for (int j = 1; j <= ncoef; j++)
                                        {
                                            xmin[j] = wk[j];
                                            xmax[j] = wk[j];
                                        }
                                    }
                                }
                                for (int i = 1; i <= ncoef; i++)
                                {
                                    double temp = wk[i];
                                    if (temp < xmin[i])
                                        xmin[i] = temp;
                                    if (temp > xmax[i])
                                        xmax[i] = temp;
                                }
                            }
                            else
                            {
                                for (int i = intp1; i <= ncoef; i++)
                                {
                                    double temp = wk[i];
                                    if (temp == xmin[i])
                                        ifault = 10;
                                    if (temp == xmax[i])
                                        ifault = 11;
                                }
                            }
                            if (wt != 0.0)
                            {
                                // sd2 is the weight of the record times its frequency: the scale factor that the record comes with
                                double sd2 = wt * frq;
                                // With isub the first row holds the sum of the weights and the weighted sums.  The record has the means so far, itself
                                // included, taken from it, and its scale factor is adjusted to match, before it is rotated into the other rows.
                                if (isub == 1)
                                {
                                    double sumwt = r[1, 1];
                                    r[1, 1] = sumwt + sd2;
                                    if (nind > 0)
                                    {
                                        for (int i = 2; i <= nind + 1; i++)
                                            r[1, i] += sd2 * wk[i];
                                    }
                                    for (int i = 1; i <= ndep; i++)
                                        b2[1, i] += sd2 * wk[idepx - 1 + i];
                                    skip = false;
                                    if (r[1, 1] != 0.0)
                                    {
                                        d[1] = 1.0 / r[1, 1];
                                        if (nind > 0)
                                            for (int i = 2; i <= nind + 1; i++)
                                                wk[i] -= d[1] * r[1, i];
                                        int j = idepx;
                                        for (int i = 1; i <= ndep; i++)
                                        {
                                            wk[j] -= d[1] * b2[1, i];
                                            j++;
                                        }
                                        if (sumwt == 0.0)
                                            skip = true;
                                        else
                                            sd2 *= r[1, 1] / sumwt;
                                    }
                                    else
                                    {
                                        skip = true;
                                    }
                                }
                                if (!skip)
                                {
                                    // The record is rotated into the triangle a row at a time.  drotmg finds the rotation that clears element i of the
                                    // record against the diagonal of row i, and drotm_21 applies it to the rest of the row and of the record, and to
                                    // the dependent variables.  What is left of the dependent variable at the end is the record's share of the
                                    // residual sum of squares.
                                    for (int i = i1; i <= ncoef; i++)
                                    {
                                        drotmg(ref d[i], ref sd2, ref r[i, i], wk[i], sparam);
                                        drotm_21(ndep, b2, i, 1, wk, idepx, sparam);
                                        if (i != ncoef)
                                            drotm_21(ncoef - i, r, i, i + 1, wk, i + 1, sparam);
                                    }
                                    int jdepjx = idepx;
                                    int jdepix = idepx;
                                    scpe += wk[jdepix] * sd2 * wk[jdepjx];
                                    // jdepix = jdepix + 1; 
                                    // jdepjx = jdepjx + 1; 
                                }
                            }
                        }
                    }
                }
            }

            // drop collinear variables
            // A predictor is dropped if it does not vary (it is zero throughout, or is a second constant), or if what is left of it after the
            // predictors before it, d r^2 on the diagonal, is no more than tol^2 of the whole of its column below the row of the constant.  Its row
            // is rotated out against the rows below it, its share of the dependent variable goes to the residual sum of squares, and the row is
            // set to zero.
            if (ido == 0 || ido == 3)
            {
                int nconst = 0;
                for (int i = 1; i <= ncoef; i++)
                {
                    int ldep = 0;
                    int k;
                    if (xmin[i] == xmax[i])
                    {
                        if (xmin[i] == 0.0)
                        {
                            ldep = 1;
                        }
                        else
                        {
                            nconst++;
                            if (nconst > 1)
                                ldep = 1;
                        }
                    }
                    else if (i > intp1)
                    {
                        double temp = 0.0;
                        k = intp1;
                        for (int j = 1; j <= i - intcep; j++)
                        {
                            temp += r[k, i] * d[k] * r[k, i];
                            k++;
                        }
                        if (d[i] * r[i, i] * r[i, i] <= tolsq * temp)
                            ldep = 1;
                    }
                    if (ldep == 1)
                    {
                        for (int j = i + 1; j <= ncoef; j++)
                        {
                            drotmg(ref d[j], ref d[i], ref r[j, j], r[i, j], sparam);
                            drotm_22(ndep, b2, j, 1, b2, i, 1, sparam);
                            if (j != ncoef)
                                drotm_22(ncoef - j, r, j, j + 1, r, i, j + 1, sparam);
                        }
                        scpe += b2[i, 1] * d[i] * b2[i, 1];
                        for (k = 1; k <= ndep; k++)
                            b2[i, k] = 0.0;
                        for (k = 0; k <= ncoef - i; k++)
                            r[i, i + k] = 0.0;
                    }
                }

                // calculate b by back-substitution
                // R b = the dependent variable as the rotations have left it, solved by mxinv2.  The scale factors are then taken into R: each row is
                // multiplied by the square root of its d, with the sign that makes the diagonal positive, and d is set to ones.
                for (int i = 1; i <= ncoef; i++)
                    b[i] = b2[i, 1];
                mxinv2(ncoef, r, b, true, false, false, r, out irank, ref ifault);
                dfe -= irank;
                if (dfe <= 0.0)
                    ifault = 12;
                for (int i = 1; i <= ncoef; i++)
                    d[i] = dsign(Math.Sqrt(d[i]), r[i, i]);
                for (int i = 1; i <= ncoef; i++)
                    for (int j = 0; j <= ncoef - i; j++)
                        r[i, i + j] *= d[i];
                for (int i = 1; i <= ncoef; i++)
                    d[i] = 1.0;
            }
        }

        /// <summary>
        /// The same as glsqr, for data held in a vector, one column after another: element (record, column) is at x[record + (column - 1) ldx].
        /// </summary>
        /// <remarks>See glsqr, of which this is a copy but for the way in which x is read.</remarks>
        public static void glsqr1(int ido, int intcep, int isub, int nrow, int nvar, double[] x, int ldx, int iind, int[] indind, int idep, int[] inddep, int ifrq, int iwt, double[] b, double[,] r, double[] d, ref int irank, ref double dfe, ref double scpe, ref int nrmiss, double[] xmin, double[] xmax, double[] wk, ref int ifault)
        {
            double[] sparam = new double[5 + 1];

            if (ifault != 0)
                return;

            // The coefficients are the constant, if there is one, and then the predictors; in wk the dependent variables follow them, from idepx
            int ndep = Math.Abs(idep);
            int nind = Math.Abs(iind);
            int ncoef = intcep + nind;
            int intp1 = intcep + 1;
            int idepx = ncoef + 1;

            double[,] b2 = new double[ncoef + 1, ncoef + 1];

            // tolerance of 100 * largest relative spacing
            const double tol = 100.0 * Constant.EPSILON;
            const double tolsq = tol * tol;

            // A fresh start: nothing in R, and the scale factors at one
            if (ido <= 1)
            {
                nrmiss = 0;
                dfe = 0.0;
                for (int i = 1; i <= ncoef; i++)
                    for (int j = 1; j <= ncoef; j++)
                        r[j, i] = 0.0;
                for (int i = 1; i <= ndep; i++)
                    for (int j = 1; j <= ncoef; j++)
                        b2[j, i] = 0.0;
                for (int j = 1; j <= ncoef; j++)
                {
                    d[j] = 1.0;
                    xmin[j] = Constant.MISSING;
                    xmax[j] = Constant.MISSING;
                }
                scpe = 0.0;
            }

            // A negative nrow takes records out of the fit: irow is then -1, and the frequency of each record is made negative
            int nobs;
            int irow;
            if (nrow < 0)
            {
                nobs = -nrow;
                irow = -1;
            }
            else
            {
                nobs = nrow;
                irow = 1;
            }
            int i1 = isub == 0 ? 1 : 2;

            double frq = 0;
            double wt = 0;
            // A record at a time: the record is put in wk and looked at for missing values, its range is noted, and it is rotated into the triangle.
            // A record with a missing value is passed over, and counted in nrmiss.
            for (int iobs = 1; iobs <= nobs; iobs++)
            {
                CheckObs1(ido, x, ldx, iobs, irow, ifrq, iwt, Constant.MISSING, ref nrmiss, ref frq, ref wt, out int igo, ref ifault);
                if (igo == 3)
                    return;
                if (igo != 2 && igo != 1)
                {
                    if (intcep == 1)
                        wk[1] = 1.0;
                    for (int i = 1; i <= iind; i++)
                        wk[intcep + i] = x[iobs + (indind[i] - 1) * ldx];
                    for (int i = 1; i <= -iind; i++)
                        wk[intcep + i] = x[iobs + (i - 1) * ldx];
                    // the predictors are elements intp1 to intcep + nind of wk (the search had stopped at element nind)
                    if (IndexNaN(intcep + nind, wk, intp1) > 0)
                    {
                        nrmiss += irow;
                    }
                    else
                    {
                        int jdepx = idepx;
                        for (int i = 1; i <= idep; i++)
                        {
                            wk[jdepx] = x[iobs + (inddep[i] - 1) * ldx];
                            jdepx++;
                        }
                        for (int i = idep + 1; i <= 0; i++)
                        {
                            wk[jdepx] = x[iobs + (nvar + i - 1) * ldx];
                            jdepx++;
                        }
                        // the dependent variables are elements idepx to idepx + ndep - 1 (the search had stopped at element ndep, before it began)
                        if (IndexNaN(idepx + ndep - 1, wk, idepx) > 0)
                        {
                            nrmiss += irow;
                        }
                        else
                        {
                            dfe += frq;
                            if (irow == 1)
                            {
                                if (ncoef > 0)
                                {
                                    if (xmin[1] == Constant.MISSING)
                                    {
                                        for (int j = 1; j <= ncoef; j++)
                                        {
                                            xmin[j] = wk[j];
                                            xmax[j] = wk[j];
                                        }
                                    }
                                }
                                for (int i = 1; i <= ncoef; i++)
                                {
                                    double temp = wk[i];
                                    if (temp < xmin[i])
                                        xmin[i] = temp;
                                    if (temp > xmax[i])
                                        xmax[i] = temp;
                                }
                            }
                            else
                            {
                                for (int i = intp1; i <= ncoef; i++)
                                {
                                    double temp = wk[i];
                                    if (temp == xmin[i])
                                        ifault = 10;
                                    if (temp == xmax[i])
                                        ifault = 11;
                                }
                            }
                            if (wt != 0.0)
                            {
                                // sd2 is the weight of the record times its frequency: the scale factor that the record comes with
                                double sd2 = wt * frq;
                                bool skip = false;
                                // With isub the first row holds the sum of the weights and the weighted sums.  The record has the means so far, itself
                                // included, taken from it, and its scale factor is adjusted to match, before it is rotated into the other rows.
                                if (isub == 1)
                                {
                                    double sumwt = r[1, 1];
                                    r[1, 1] = sumwt + sd2;
                                    if (nind > 0)
                                    {
                                        for (int i = 2; i <= nind + 1; i++)
                                            r[1, i] += sd2 * wk[i];
                                    }
                                    for (int i = 1; i <= ndep; i++)
                                        b2[1, i] += sd2 * wk[idepx - 1 + i];
                                    skip = false;
                                    if (r[1, 1] != 0.0)
                                    {
                                        d[1] = 1.0 / r[1, 1];
                                        if (nind > 0)
                                        {
                                            for (int i = 2; i <= nind + 1; i++)
                                                wk[i] -= d[1] * r[1, i];
                                        }
                                        int j = idepx;
                                        for (int i = 1; i <= ndep; i++)
                                        {
                                            wk[j] -= d[1] * b2[1, i];
                                            j++;
                                        }
                                        if (sumwt == 0.0)
                                        {
                                            skip = true;
                                        }
                                        else
                                        {
                                            sd2 *= r[1, 1] / sumwt;
                                        }
                                    }
                                    else
                                    {
                                        skip = true;
                                    }
                                }
                                if (!skip)
                                {
                                    // The record is rotated into the triangle a row at a time.  drotmg finds the rotation that clears element i of the
                                    // record against the diagonal of row i, and drotm_21 applies it to the rest of the row and of the record, and to
                                    // the dependent variables.  What is left of the dependent variable at the end is the record's share of the
                                    // residual sum of squares.
                                    for (int i = i1; i <= ncoef; i++)
                                    {
                                        drotmg(ref d[i], ref sd2, ref r[i, i], wk[i], sparam);
                                        drotm_21(ndep, b2, i, 1, wk, idepx, sparam);
                                        if (i != ncoef)
                                            drotm_21(ncoef - i, r, i, i + 1, wk, i + 1, sparam);
                                    }
                                    int jdepjx = idepx;
                                    int jdepix = idepx;
                                    scpe += wk[jdepix] * sd2 * wk[jdepjx];
                                    // jdepix = jdepix + 1; 
                                    // jdepjx = jdepjx + 1; 
                                }
                            }
                        }
                    }
                }
            }

            // drop collinear variables
            // A predictor is dropped if it does not vary (it is zero throughout, or is a second constant), or if what is left of it after the
            // predictors before it, d r^2 on the diagonal, is no more than tol^2 of the whole of its column below the row of the constant.  Its row
            // is rotated out against the rows below it, its share of the dependent variable goes to the residual sum of squares, and the row is
            // set to zero.
            if (ido == 0 || ido == 3)
            {
                int nconst = 0;
                for (int i = 1; i <= ncoef; i++)
                {
                    int ldep = 0;
                    int k;
                    if (xmin[i] == xmax[i])
                    {
                        if (xmin[i] == 0.0)
                        {
                            ldep = 1;
                        }
                        else
                        {
                            nconst++;
                            if (nconst > 1)
                                ldep = 1;
                        }
                    }
                    else if (i > intp1)
                    {
                        double temp = 0.0;
                        k = intp1;
                        for (int j = 1; j <= i - intcep; j++)
                        {
                            temp += r[k, i] * d[k] * r[k, i];
                            k++;
                        }
                        if (d[i] * r[i, i] * r[i, i] <= tolsq * temp)
                            ldep = 1;
                    }
                    if (ldep == 1)
                    {
                        for (int j = i + 1; j <= ncoef; j++)
                        {
                            drotmg(ref d[j], ref d[i], ref r[j, j], r[i, j], sparam);
                            drotm_22(ndep, b2, j, 1, b2, i, 1, sparam);
                            if (j != ncoef)
                                drotm_22(ncoef - j, r, j, j + 1, r, i, j + 1, sparam);
                        }
                        scpe += b2[i, 1] * d[i] * b2[i, 1];
                        for (k = 1; k <= ndep; k++)
                            b2[i, k] = 0.0;
                        for (k = 0; k <= ncoef - i; k++)
                            r[i, i + k] = 0.0;
                    }
                }

                // calculate b by back-substitution
                // R b = the dependent variable as the rotations have left it, solved by mxinv2.  The scale factors are then taken into R: each row is
                // multiplied by the square root of its d, with the sign that makes the diagonal positive, and d is set to ones.
                for (int i = 1; i <= ncoef; i++)
                    b[i] = b2[i, 1];
                mxinv2(ncoef, r, b, true, false, false, r, out irank, ref ifault);
                dfe -= irank;
                if (dfe <= 0.0)
                    ifault = 12;
                for (int i = 1; i <= ncoef; i++)
                    d[i] = dsign(Math.Sqrt(d[i]), r[i, i]);
                for (int i = 1; i <= ncoef; i++)
                    for (int j = 0; j <= ncoef - i; j++)
                        r[i, i + j] = r[i, i + j] * d[i];
                for (int i = 1; i <= ncoef; i++)
                    d[i] = 1.0;
            }
        }

        /// <summary>
        /// Returns x with y's sign.
        /// </summary>
        private static double dsign(double x, double y)
        {
            return y < 0.0 ? -Math.Abs(x) : Math.Abs(x);
        }

        /// <summary>
        /// Reads the frequency and the weight of a record, and says what is to be done with the record.
        /// </summary>
        /// <remarks>
        /// ifrq and iwt are the columns of x that hold the frequency and the weight, or 0 where there is none, when the value is 1.  The frequency is
        /// made negative when irow is -1, which takes the record out of a fit.  igo is 0 if the record is to be used; 1 if it is to be passed over
        /// because its frequency is zero; 2 if it is to be passed over because its frequency or weight is missing, when it is counted in nmiss; and
        /// 3 if the fit is to stop because its frequency or weight is negative, with fault 2 or 3 for a frequency and 5 or 6 for a weight.  xmiss is
        /// not used: the missing value is Constant.MISSING.
        /// </remarks>
        private static void CheckObs(int ido, double[,] x, int iobs, int irow, int ifrq, int iwt, double xmiss, ref int nmiss, ref double frq, ref double wt, out int igo, ref int ifault)
        {
            igo = 0;
            if (ifrq > 0)
            {
                frq = x[iobs, ifrq];
                if (frq == Constant.MISSING)
                {
                    nmiss += irow;
                    igo = 2;
                }
                else if (frq == 0.0)
                {
                    igo = 1;
                    return;
                }
            }
            if (iwt > 0)
            {
                wt = x[iobs, iwt];
                if (wt == Constant.MISSING)
                {
                    if (igo != 2)
                    {
                        nmiss += irow;
                        igo = 2;
                    }
                }
            }
            if (ifrq > 0)
            {
                // a frequency that is there must not be negative (the test had been made on a missing one only)
                if (frq != Constant.MISSING)
                {
                    if (frq < 0.0)
                    {
                        ifault = ido > 0 ? 2 : 3;
                        igo = 3;
                        return;
                    }
                }
            }
            else
            {
                frq = 1.0;
            }
            if (irow == -1)
                frq = -frq;
            if (iwt > 0)
            {
                // nor may a weight
                if (wt != Constant.MISSING)
                {
                    if (wt < 0.0)
                    {
                        ifault = ido > 0 ? 5 : 6;
                        igo = 3;
                    }
                }
            }
            else
            {
                wt = 1.0;
            }
        }

        /// <summary>
        /// The same as CheckObs, for data held in a vector, one column after another: element (record, column) is at x[record + (column - 1) ldx].
        /// </summary>
        private static void CheckObs1(int ido, double[] x, int ldx, int iobs, int irow, int ifrq, int iwt, double xmiss, ref /* Yes, really */ int nmiss, ref /* yes, really */ double frq, ref double wt, out int igo, ref int ifault)
        {
            igo = 0;
            if (ifrq > 0)
            {
                frq = x[iobs + (ifrq - 1) * ldx];
                if (frq == Constant.MISSING)
                {
                    nmiss += irow;
                    igo = 2;
                }
                else if (frq == 0.0)
                {
                    igo = 1;
                    return;
                }
            }
            if (iwt > 0)
            {
                wt = x[iobs + (iwt - 1) * ldx];
                if (wt == Constant.MISSING)
                {
                    if (igo != 2)
                    {
                        nmiss += irow;
                        igo = 2;
                    }
                }
            }
            if (ifrq > 0)
            {
                // a frequency that is there must not be negative (the test had been made on a missing one only)
                if (frq != Constant.MISSING)
                {
                    if (frq < 0.0)
                    {
                        ifault = ido > 0 ? 2 : 3;
                        igo = 3;
                        return;
                    }
                }
            }
            else
            {
                frq = 1.0;
            }
            if (irow == -1)
                frq = -frq;
            if (iwt > 0)
            {
                // nor may a weight
                if (wt != Constant.MISSING)
                {
                    if (wt < 0.0)
                    {
                        ifault = ido > 0 ? 5 : 6;
                        igo = 3;
                    }
                }
            }
            else
            {
                wt = 1.0;
            }
        }

        /// <summary>
        /// Returns the smallest index of sx[ix..n] = nan, or 0 if none found.
        /// </summary>
        /// <remarks>n is the last element that is looked at, not the number of elements.  The value looked for is Constant.MISSING.</remarks>
        /// <param name="n">The last index to be searched</param>
        /// <param name="sx">The vector to be searched</param>
        /// <param name="ix">First index to be searched</param>
        /// <returns></returns>
        private static int IndexNaN(int n, double[] sx, int ix)
        {
            if (n >= 0)
                for (int i = ix; i <= n; i++)
                    if (sx[i] == Constant.MISSING)
                        return i;
            return 0;
        }

        /// <summary>
        /// blas modified givens rotations application
        /// </summary>
        /// <remarks>
        /// Applies the rotation H that drotmg has left in sparam to n pairs of elements: w from row ix1 of sx, from column ix2 on, and z from sy, from
        /// element iy on.  w becomes h11 w + h12 z and z becomes h21 w + h22 z.  sparam[1] says which elements of H are held and which are taken as
        /// read: -2 for H = I, when nothing is done; 0 for h11 = h22 = 1; 1 for h12 = 1 and h21 = -1; -1 for all four held.  sparam[2] to sparam[5]
        /// are h11, h21, h12 and h22.  BLAS calls this DROTM.
        /// </remarks>
        private static void drotm_21(int n, double[,] sx, int ix1, int ix2, double[] sy, int iy, double[] sparam)
        {
            double sflag = sparam[1];
            if (n > 0 && sflag != -2.0)
            {
                int i;
                double sh12;
                double z;
                double w;
                double sh21;
                if (sflag == 0.0)
                {
                    sh12 = sparam[4];
                    sh21 = sparam[3];
                    for (i = 0; i < n; i++)
                    {
                        w = sx[ix1, ix2 + i];
                        z = sy[iy + i];
                        sx[ix1, ix2 + i] = w + z * sh12;
                        sy[iy + i] = w * sh21 + z;
                    }
                }
                else
                {
                    double sh11;
                    double sh22;
                    if (sflag > 0.0)
                    {
                        sh11 = sparam[2];
                        sh22 = sparam[5];
                        for (i = 0; i < n; i++)
                        {
                            w = sx[ix1, ix2 + i];
                            z = sy[iy + i];
                            sx[ix1, ix2 + i] = w * sh11 + z;
                            sy[iy + i] = -w + sh22 * z;
                        }
                    }
                    else if (sflag < 0.0)
                    {
                        sh11 = sparam[2];
                        sh12 = sparam[4];
                        sh21 = sparam[3];
                        sh22 = sparam[5];
                        for (i = 0; i < n; i++)
                        {
                            w = sx[ix1, ix2 + i];
                            z = sy[iy + i];
                            sx[ix1, ix2 + i] = w * sh11 + z * sh12;
                            sy[iy + i] = w * sh21 + z * sh22;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// blas modified givens rotations application
        /// </summary>
        /// <remarks>
        /// The same as drotm_21, with z as well as w taken from a row of a matrix: row iy1 of sy, from column iy2 on.
        /// </remarks>
        private static void drotm_22(int n, double[,] sx, int ix1, int ix2, double[,] sy, int iy1, int iy2, double[] sparam)
        {
            double sflag = sparam[1];
            if (n > 0 & sflag != -2.0)
            {
                if (sflag == 0.0)
                {
                    double sh12 = sparam[4];
                    double sh21 = sparam[3];
                    for (int i = 0; i < n; i++)
                    {
                        double w = sx[ix1, ix2 + i];
                        double z = sy[iy1, iy2 + i];
                        sx[ix1, ix2 + i] = w + z * sh12;
                        sy[iy1, iy2 + i] = w * sh21 + z;
                    }
                }
                else
                {
                    if (sflag > 0.0)
                    {
                        double sh11 = sparam[2];
                        double sh22 = sparam[5];
                        for (int i = 0; i < n; i++)
                        {
                            double w = sx[ix1, ix2 + i];
                            double z = sy[iy1, iy2 + i];
                            sx[ix1, ix2 + i] = w * sh11 + z;
                            sy[iy1, iy2 + i] = -w + sh22 * z;
                        }
                    }
                    else if (sflag < 0.0)
                    {
                        double sh11 = sparam[2];
                        double sh12 = sparam[4];
                        double sh21 = sparam[3];
                        double sh22 = sparam[5];
                        for (int i = 0; i < n; i++)
                        {
                            double w = sx[ix1, ix2 + i];
                            double z = sy[iy1, iy2 + i];
                            sx[ix1, ix2 + i] = w * sh11 + z * sh12;
                            sy[iy1, iy2 + i] = w * sh21 + z * sh22;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Finds the modified rotation that clears y against x, for two rows that are held with the scale factors d1 and d2.
        /// </summary>
        /// <remarks>
        /// A row is held as the square root of its scale factor times what is stored, so the pair that is rotated is (sqrt(d1) x, sqrt(d2) y).  The
        /// rotation H, which is left in p for drotm_21 and drotm_22, makes the second of the pair zero without a square root being taken: on return
        /// d1, d2 and x have been brought up to date, and d1 x^2 is what d1 x^2 + d2 y^2 was.  Of the two forms of H the one that is used is that with
        /// its larger elements equal to 1 (p[1] is 0 if |d1 x^2| is more than |d2 y^2|, when h11 = h22 = 1, and 1 if not, when h12 = 1 and
        /// h21 = -1).  If d2 y is zero nothing needs doing, and p[1] is -2.  If a scale factor is out of the range 1 / g^2 to g^2, with g = 4096, it is
        /// brought back in by factors of g^2, the elements of H take up the factors of g, and p[1] is -1: all four elements are then held.  A
        /// negative d1, or a rotation that cannot be made, sets H and the scale factors to zero.  BLAS calls this DROTMG.
        /// </remarks>
        private static void drotmg(ref double d1, ref double d2, ref double x, double y, double[] p)
        {

            //      blas modified givens rotations

            double u, h21, h11, h12, h22;

            const double g = 4096;
            const double g2 = g * g;

            if (d1 < 0.0)
            {
                //  d1 < 0
                p[1] = -1.0;
                p[2] = 0.0;
                p[3] = 0.0;
                p[4] = 0.0;
                p[5] = 0.0;
                d1 = 0.0;
                d2 = 0.0;
                x = 0.0;
                return;
            }
            if (d2 * y == 0.0)
            {
                //  h = i
                p[1] = -2.0;
                return;
            }
            if (Math.Abs(d1 * x * x) > Math.Abs(d2 * y * y))
            {
                //  equation a6
                p[1] = 0.0;
                h11 = 1.0;
                h12 = d2 * y / (d1 * x);
                h21 = -y / x;
                h22 = 1.0;
                u = 1.0 - h21 * h12;
                if (u <= 0.0)
                {
                    //  reject u <= 0
                    p[1] = -1.0;
                    p[2] = 0.0;
                    p[3] = 0.0;
                    p[4] = 0.0;
                    p[5] = 0.0;
                    d1 = 0.0;
                    d2 = 0.0;
                    x = 0.0;
                    return;
                }
                d1 /= u;
                d2 /= u;
                x *= u;
            }
            else
            {
                //  equation a7
                if (d2 * y * y < 0.0)
                {
                    p[1] = -1.0;
                    p[2] = 0.0;
                    p[3] = 0.0;
                    p[4] = 0.0;
                    p[5] = 0.0;
                    d1 = 0.0;
                    d2 = 0.0;
                    x = 0.0;
                    return;
                }
                p[1] = 1.0;
                h11 = d1 * x / (d2 * y);
                h12 = 1.0;
                h21 = -1.0;
                h22 = x / y;
                u = 1.0 + h11 * h22;
                d1 /= u;
                d2 /= u;
                double tmp = d2;
                d2 = d1;
                d1 = tmp;
                x = y * u;
            }

            //  rescale d1 in the range rg2, g2
            while (d1 <= 1.0 / g2 && d1 != 0.0)
            {
                p[1] = -1.0;
                d1 *= g2;
                x /= g;
                h11 /= g;
                h12 /= g;
            }
            while (d1 >= g2)
            {
                p[1] = -1.0;
                d1 /= g2;
                x *= g;
                h11 *= g;
                h12 *= g;
            }
            //  rescale d2 in the range rg2, g2
            while (Math.Abs(d2) <= 1.0 / g2 & d2 != 0.0)
            {
                p[1] = -1.0;
                d2 *= g2;
                h21 /= g;
                h22 /= g;
            }
            while (Math.Abs(d2) >= g2)
            {
                p[1] = -1.0;
                d2 /= g2;
                h21 *= g;
                h22 *= g;
            }
            //  populate the parameter array with rescaled values
            if (p[1] == -1.0)
            {
                p[2] = h11;
                p[3] = h21;
                p[4] = h12;
                p[5] = h22;
            }
            else if (p[1] == 0.0)
            {
                p[3] = h21;
                p[4] = h12;
            }
            else if (p[1] == 1.0)
            {
                p[2] = h11;
                p[5] = h22;
            }
        }

        ///  <summary>
        ///  variance-covariance matrix from the r matrix
        ///  </summary>
        ///  <param name="ncoef">The number of coefficients.</param>
        ///  <param name="r">R, upper triangular, as glsqr leaves it.</param>
        ///  <param name="s2">The residual mean square, or 1 for the inverse of R'R itself.</param>
        ///  <param name="covb">On return, s2 (R'R)^-1, the whole symmetric matrix.</param>
        ///  <param name="ifault">On entry not zero, nothing is done; on return, a fault of mxinv2.</param>
        ///  <remarks>
        ///  The inverse V of R is found by mxinv2, and the product V V' is then formed a column at a time in the upper triangle, as dpptri forms it
        ///  in RegressRpt.cs.  The row and column of a predictor that glsqr dropped, whose diagonal element of R is zero, are zero.
        ///  </remarks>
        public static void rcovarb(int ncoef, double[,] r, double s2, double[,] covb, ref int ifault)
        {
            mxinv2(ncoef, r, null, false, false, true, covb, out int _, ref ifault);

            if (ifault != 0)
                return;

            for (int j = 1; j <= ncoef; j++)
            {
                if (covb[j, j] > 0.0)
                {
                    double t;
                    for (int k = 1; k < j; k++)
                    {
                        t = covb[k, j];
                        for (int i = 1; i <= k; i++)
                            covb[i, k] = covb[i, k] + covb[i, j] * t;
                    }
                    t = covb[j, j];
                    for (int k = 1; k <= j; k++)
                        covb[k, j] = covb[k, j] * t;
                }
                else
                {
                    for (int k = 1; k <= j; k++)
                        covb[k, j] = 0.0;
                }
            }

            for (int j = 1; j <= ncoef; j++)
                for (int k = 1; k <= j; k++)
                    covb[k, j] = covb[k, j] * s2;

            // fill in the lower triangle
            for (int i = 1; i < ncoef; i++)
                for (int j = i + 1; j <= ncoef; j++)
                    covb[j, i] = covb[i, j];
        }

        ///  <summary>
        ///  Solve a set of linear systems and/or compute a generalized inverse of upper triangular matrix
        ///  </summary>
        ///  <param name="n">The order of R.</param>
        ///  <param name="r">R, in the upper triangle; what is below the diagonal is not looked at.</param>
        ///  <param name="b">The right-hand side on entry, and the solution on return, if useB is set.</param>
        ///  <param name="useB">true for all cases of old paths 1-4</param>
        ///  <param name="transposeR">true to transpose (old path 2 or 4)</param>
        ///  <param name="invertR">true for old paths 3, 4</param>
        ///  <param name="rinv">On return the inverse of R, upper triangular, if invertR is set.</param>
        ///  <param name="irank">On return, the number of diagonal elements of R that are not zero.</param>
        ///  <param name="ifault">
        ///  On entry not zero, nothing is done.  On return 5 if a row of R has a diagonal element of zero and something else beside it, and 1 or 2 if
        ///  the equations cannot all be met because R is not of full rank.
        ///  </param>
        ///  <remarks>
        ///  With useB, R x = b is solved, or R'x = b if transposeR is set, by substitution.  A row of R that is wholly zero stands for a predictor that
        ///  was dropped: its element of the solution is zero, and its row and column of the inverse are zero, which makes the inverse a generalised
        ///  one.  LAPACK solves with DTRTRS and inverts with DTRTRI.
        ///  </remarks>
        public static void mxinv2(int n, double[,] r, double[] b, bool useB, bool transposeR, bool invertR, double[,] rinv, out int irank, ref int ifault)
        {
            if (ifault != 0)
            {
                irank = 0;
                return;
            }
            for (int i = 1; i <= n; i++)
            {
                if (r[i, i] == 0.0)
                {
                    for (int j = i + 1; j <= n; j++)
                    {
                        if (r[i, j] != 0.0)
                        {
                            ifault = 5;
                            irank = 0;
                            return;
                        }
                    }
                }
            }
            if (ifault != 0)
            {
                irank = 0;
                return;
            }

            irank = 0;
            for (int i = 1; i <= n; i++)
                if (r[i, i] != 0.0)
                    irank += 1;

            if (transposeR)
            {
                if (irank < n)
                {
                    if (useB)
                    {
                        for (int j = 1; j <= n; j++)
                        {
                            double xddot = 0.0;
                            for (int k = 1; k < j; k++)
                                xddot += r[k, j] * b[k];
                            double temp1 = b[j] - xddot;
                            if (r[j, j] == 0.0)
                            {
                                double absprod = 0.0;
                                for (int ii = 1; ii < j; ii++)
                                    absprod += Math.Abs(r[ii, j] * b[ii]);
                                double temp2 = Math.Abs(b[j]) + absprod;
                                temp2 *= 200.0 * Constant.EPSILON;
                                if (Math.Abs(temp1) > temp2)
                                    ifault = 2;
                                b[j] = 0.0;
                            }
                            else
                            {
                                b[j] = temp1 / r[j, j];
                            }
                        }
                    }
                }
                else
                {
                    if (useB)
                    {
                        for (int j = 1; j <= n; j++)
                        {
                            double xddot = 0.0;
                            for (int k = 1; k < j; k++)
                                xddot += r[k, j] * b[k];
                            b[j] = b[j] - xddot;
                            b[j] = b[j] / r[j, j];
                        }
                    }
                }
            }
            else
            {
                if (irank < n)
                {
                    if (useB)
                    {
                        for (int j = n; j >= 1; j--)
                        {
                            if (r[j, j] == 0.0)
                            {
                                if (b[j] != 0.0)
                                    ifault = 1;
                                b[j] = 0.0;
                            }
                            else
                            {
                                b[j] = b[j] / r[j, j];
                                double temp1 = -b[j];
                                for (int k = 1; k < j; k++)
                                    b[k] = b[k] + temp1 * r[k, j];
                            }
                        }
                    }
                }
                else
                {
                    if (useB)
                    {
                        for (int j = n; j >= 1; j--)
                        {
                            if (j < n)
                            {
                                double xddot = 0.0;
                                for (int k = 1; k <= n - j; k++)
                                    xddot += r[j, j + k] * b[j + k];
                                b[j] = b[j] - xddot;
                            }
                            b[j] = b[j] / r[j, j];
                        }
                    }
                }
            }

            if (invertR)
            {
                for (int j = 1; j <= n; j++)
                    for (int k = 1; k <= j; k++)
                        rinv[k, j] = r[k, j];
                for (int k = 1; k <= n; k++)
                {
                    if (rinv[k, k] == 0.0)
                    {
                        for (int i = 1; i <= k; i++)
                            rinv[i, k] = 0.0;
                        if (n != k)
                            for (int i = 1; i <= n - k; i++)
                                rinv[k, k + i] = 0.0;
                    }
                    else
                    {
                        rinv[k, k] = 1.0 / rinv[k, k];
                        double temp1 = -rinv[k, k];
                        for (int i = 1; i < k; i++)
                            rinv[i, k] = rinv[i, k] * temp1;
                        if (k < n)
                        {
                            for (int j = 1; j <= n - k; j++)
                                for (int i = 1; i < k; i++)
                                    rinv[i, k + j] = rinv[i, k + j] + rinv[k, k + j] * rinv[i, k];
                            for (int i = 1; i <= n - k; i++)
                                rinv[k, k + i] = rinv[k, k + i] * rinv[k, k];
                        }
                    }
                }
                for (int i = 1; i < n; i++)
                    for (int ii = i + 1; ii <= n; ii++)
                        rinv[ii, i] = 0.0;
            }
        }

        /// <summary>
        /// Determines the singular value decomposition a=usv  of a real m by n rectangular matrix.  householder bidiagonalization and a variant of the qr algorithm are used.
        /// </summary>
        /// <param name="m">number of rows of a (and u)</param>
        /// <param name="n">number of columns of a (and u) and the order of v</param>
        /// <param name="w">Postcondition: w contains the n (non-negative) singular values of a (the diagonal elements of s).  they are unordered.  if an error exit is made, the singular values should be correct for indices ierr+1,ierr+2,...,n.</param>
        /// <param name="u">u contains the matrix u (orthogonal column vectors) of the decomposition if matu has been set to true otherwise u is used as a temporary array.  u may coincide with a.  if an error exit is made, the columns of u corresponding to indices of correct singular values should be correct.</param>
        /// <param name="v">v contains the matrix v (orthogonal) of the decomposition if matv has been set to true otherwise v is not referenced.  v may also coincide with a if u is not needed.  if an error exit is made, the columns of v corresponding to indices of correct singular values should be correct.</param>
        /// <returns>set to zero for normal return, k if the k-th singular value has not been determined after 30 iterations</returns>
        /// <remarks>This subroutine is a translation of the algol procedure svd, num. math. 14, 403-420(1970) by golub and reinsch. handbook for auto. comp., vol ii-linear algebra, 134-151(1971).
        /// Questions and comments should be directed to burton s. garbow,  mathematics and computer science div, argonne national laboratory. this version dated august 1983.</remarks>
        private static int SingularValueDecomposition(int m, int n, double[] w, double[,] u, double[,] v)
        {
            int l = 0, l1 = 0;
            double f, h, s;


            //         a contains the rectangular input matrix to be decomposed.

            //      on output
            //         a is unaltered (unless overwritten by u or v).

            //      calls pythag for  dsqrt(a*a + b*b) .
            // Householder reduction to bidiagonal form
            double g = 0.0;
            double scale = 0.0;
            double x = 0.0;
            double[] rv1 = new double[n + 1];
            for (int i = 1; i <= n; i++)
            {
                l = i + 1;
                rv1[i] = scale * g;
                g = 0.0;
                s = 0.0;
                scale = 0.0;
                if (i <= m)
                {
                    for (int k = i; k <= m; k++)
                        scale += Math.Abs(u[k, i]);
                    if (scale != 0.0)
                    {
                        for (int k = i; k <= m; k++)
                        {
                            u[k, i] = u[k, i] / scale;
                            s += Math.Pow(u[k, i], 2.0);
                        }
                        f = u[i, i];
                        g = -dsign(Math.Sqrt(s), f);
                        h = f * g - s;
                        u[i, i] = f - g;
                        if (i != n)
                        {
                            for (int j = l; j <= n; j++)
                            {
                                s = 0.0;
                                for (int k = i; k <= m; k++)
                                    s += u[k, i] * u[k, j];
                                f = s / h;
                                for (int k = i; k <= m; k++)
                                    u[k, j] = u[k, j] + f * u[k, i];
                            }
                        }
                        for (int k = i; k <= m; k++)
                            u[k, i] = scale * u[k, i];
                    }
                }
                w[i] = scale * g;
                g = 0.0;
                s = 0.0;
                scale = 0.0;
                if (i <= m & i != n)
                {
                    for (int k = l; k <= n; k++)
                    {
                        scale += Math.Abs(u[i, k]);
                    }
                    if (scale != 0.0)
                    {
                        for (int k = l; k <= n; k++)
                        {
                            u[i, k] = u[i, k] / scale;
                            s += Math.Pow(u[i, k], 2.0);
                        }
                        f = u[i, l];
                        g = -dsign(Math.Sqrt(s), f);
                        h = f * g - s;
                        u[i, l] = f - g;
                        for (int k = l; k <= n; k++)
                        {
                            rv1[k] = u[i, k] / h;
                        }
                        if (i != m)
                        {
                            for (int j = l; j <= m; j++)
                            {
                                s = 0.0;
                                for (int k = l; k <= n; k++)
                                    s += u[j, k] * u[i, k];
                                for (int k = l; k <= n; k++)
                                    u[j, k] = u[j, k] + s * rv1[k];
                            }
                        }
                        for (int k = l; k <= n; k++)
                            u[i, k] = scale * u[i, k];
                    }
                }
                x = Math.Max(x, Math.Abs(w[i]) + Math.Abs(rv1[i]));
            }
            // Accumulation of right-hand transformations
            //      .......... for i=n step -1 until 1 do -- ..........
            for (int ii = 1; ii <= n; ii++)
            {
                int i = n + 1 - ii;
                if (i != n)
                {
                    if (g != 0.0)
                    {
                        for (int j = l; j <= n; j++)
                        {
                            //          .......... double division avoids possible underflow ..........
                            v[j, i] = u[i, j] / u[i, l] / g;
                        }
                        for (int j = l; j <= n; j++)
                        {
                            s = 0.0;
                            for (int k = l; k <= n; k++)
                                s += u[i, k] * v[k, j];
                            for (int k = l; k <= n; k++)
                                v[k, j] = v[k, j] + s * v[k, i];
                        }
                    }
                    for (int j = l; j <= n; j++)
                    {
                        v[i, j] = 0.0;
                        v[j, i] = 0.0;
                    }
                }
                v[i, i] = 1.0;
                g = rv1[i];
                l = i;
            }
            // Accumulation of left-hand transformations
            //      ..........for i=min(m,n) step -1 until 1 do -- ..........
            int mn = n;
            if (m < n)
                mn = m;
            for (int ii = 1; ii <= mn; ii++)
            {
                int i = mn + 1 - ii;
                l = i + 1;
                g = w[i];
                if (i != n)
                    for (int j = l; j <= n; j++)
                        u[i, j] = 0.0;
                if (g != 0.0)
                {
                    if (i != mn)
                    {
                        for (int j = l; j <= n; j++)
                        {
                            s = 0.0;
                            for (int k = l; k <= m; k++)
                                s += u[k, i] * u[k, j];
                            // Double division avoids possible underflow
                            f = s / u[i, i] / g;
                            for (int k = i; k <= m; k++)
                                u[k, j] = u[k, j] + f * u[k, i];
                        }
                    }
                    for (int j = i; j <= m; j++)
                        u[j, i] = u[j, i] / g;
                }
                else
                {
                    for (int j = i; j <= m; j++)
                        u[j, i] = 0.0;
                }
                u[i, i] = u[i, i] + 1.0;
            }
            // Diagonalization of the bidiagonal form
            double tst1 = x;
            //      .......... for k=n step -1 until 1 do -- ..........
            for (int kk = 1; kk <= n; kk++)
            {
                int k1 = n - kk;
                int k = k1 + 1;
                int its = 0;
                // Test for splitting.
                //                 for l=k step -1 until 1 do -- ..........
                double z;
                do
                {
                    bool skip = false;
                    for (int ll = 1; ll <= k; ll++)
                    {
                        l1 = k - ll;
                        l = l1 + 1;
                        double tst2 = tst1 + Math.Abs(rv1[l]);
                        if (tst2 == tst1)
                        {
                            skip = true;
                            break;
                        }
                        // rv1(1) is always zero, so there is no exit through the bottom of the loop
                        tst2 = tst1 + Math.Abs(w[l1]);
                        if (tst2 == tst1)
                            break;
                    }
                    double c;
                    double y;
                    if (!skip)
                    {
                        // Cancellation of rv1(l) if l greater than 1
                        c = 0.0;
                        s = 1.0;
                        for (int i = l; i <= k; i++)
                        {
                            f = s * rv1[i];
                            rv1[i] = c * rv1[i];
                            double tst2 = tst1 + Math.Abs(f);
                            if (tst2 == tst1)
                                break;
                            g = w[i];
                            h = Pythag(f, g);
                            w[i] = h;
                            c = g / h;
                            s = -f / h;
                            for (int j = 1; j <= m; j++)
                            {
                                y = u[j, l1];
                                z = u[j, i];
                                u[j, l1] = y * c + z * s;
                                u[j, i] = -y * s + z * c;
                            }
                        }
                    }
                    // Test for convergence
                    z = w[k];
                    if (l == k)
                        break;

                    // Shift from bottom 2 by 2 minor
                    if (its >= 30)
                    {
                        // Error -- no convergence to a singular value after 30 iterations
                        return k;
                    }
                    its++;
                    x = w[l];
                    y = w[k1];
                    g = rv1[k1];
                    h = rv1[k];
                    f = 0.5 * ((g + z) / h * ((g - z) / y) + y / h - h / y);
                    g = Pythag(f, 1.0);
                    f = x - z / x * z + h / x * (y / (f + dsign(g, f)) - h);
                    // Next qr transformation
                    c = 1.0;
                    s = 1.0;
                    int i1;
                    for (i1 = l; i1 <= k1; i1++)
                    {
                        int i = i1 + 1;
                        g = rv1[i];
                        y = w[i];
                        h = s * g;
                        g = c * g;
                        z = Pythag(f, h);
                        rv1[i1] = z;
                        c = f / z;
                        s = h / z;
                        f = x * c + g * s;
                        g = -x * s + g * c;
                        h = y * s;
                        y *= c;
                        for (int j = 1; j <= n; j++)
                        {
                            x = v[j, i1];
                            z = v[j, i];
                            v[j, i1] = x * c + z * s;
                            v[j, i] = -x * s + z * c;
                        }
                        z = Pythag(f, h);
                        w[i1] = z;
                        // Rotation can be arbitrary if z is zero
                        if (z != 0.0)
                        {
                            c = f / z;
                            s = h / z;
                        }
                        f = c * g + s * y;
                        x = -s * g + c * y;
                        for (int j = 1; j <= m; j++)
                        {
                            y = u[j, i1];
                            z = u[j, i];
                            u[j, i1] = y * c + z * s;
                            u[j, i] = -y * s + z * c;
                        }
                    }
                    rv1[l] = 0.0;
                    rv1[k] = f;
                    w[k] = x;
                }
                while (true);
                // Convergence
                if (z < 0.0)
                {
                    // w(k) is made non-negative
                    w[k] = -z;
                    for (int j = 1; j <= n; j++)
                        v[j, k] = -v[j, k];
                }
            }
            return 0;
        }

        /// <summary>
        /// Finds dsqrt(a**2+b**2) without overflow or destructive underflow.
        /// </summary>
        private static double Pythag(double a, double b)
        {
            double p = Math.Max(Math.Abs(a), Math.Abs(b));
            if (p != 0.0)
            {
                double r = Math.Pow(Math.Min(Math.Abs(a), Math.Abs(b)) / p, 2.0);
                do
                {
                    double t = 4.0 + r;
                    if (t == 4.0)
                        break;
                    double s = r / t;
                    double u = 1.0 + 2.0 * s;
                    p = u * p;
                    r = Math.Pow(s / u, 2.0) * r;
                }
                while (true);
            }
            return p;
        }

        /// <summary>
        /// The Durbin-Watson statistic of the residuals: the sum of the squares of the differences between each and the one before, over the sum of
        /// the squares of the residuals.
        /// </summary>
        public static void x_dwsd(double[] er, int nx, out double dw)
        {
            double[] erd = new double[nx + 1];
            int iseas = 0;
            const int idif = 1;
            x_difd(er, erd, nx, idif, ref iseas);
            //  erd[1..nx-1] hold the nx-1 successive differences and erd[nx] is zero; all of them belong in the numerator
            double sum1 = 0.0;
            double sum2 = 0.0;
            for (int i = 1; i <= nx; i++)
            {
                sum1 += erd[i] * erd[i];
                sum2 += er[i] * er[i];
            }
            dw = sum1 / sum2;
        }

        /// <summary>
        /// The differences of a series at lag idif: yd[i] = xd[i + idif] - xd[i], and nd the number of them.
        /// </summary>
        private static void x_difd(double[] xd, double[] yd, int nx, int idif, ref int nd)
        {
            if (idif <= 0)
                return;
            int ix = idif + 1;
            nd = 0;
            for (int i = ix; i <= nx; i++)
            {
                nd++;
                yd[nd] = xd[i] - xd[i - idif];
            }
        }

        /// <summary>
        /// The half widths of the confidence interval and of the prediction interval about a fitted value.
        /// </summary>
        /// <remarks>
        /// XV is the row of the design matrix at which the prediction is made, xtxi the inverse of X'X, rms the residual mean square and cit the
        /// quantile of Student's t.  cl = cit sqrt(rms x'(X'X)^-1 x), for the mean at x, and pl = cit sqrt(rms (1 + x'(X'X)^-1 x)), for a new record.
        /// </remarks>
        public static void x_ciyp(double[] XV, double[,] xtxi, int P, double rms, double cit, out double cl, out double pl)
        {
            double xcx = 0;

            for (int i = 1; i <= P; i++)
            {
                double sLoop = 0.0;
                for (int j = 1; j <= P; j++)
                    sLoop += xtxi[i, j] * XV[j];
                xcx += sLoop * XV[i];
            }
            double sey = Math.Sqrt(rms * xcx);
            cl = cit * sey;
            double s = Math.Sqrt(rms * (1.0 + xcx));
            pl = cit * s;
        }

        ///  <summary>
        ///  trapezoidal numerical recipies p 131
        ///  </summary>
        ///  <param name="a">The lower limit.</param>
        ///  <param name="b">The upper limit.</param>
        ///  <param name="s">The result of stage n - 1; not used at stage 1.</param>
        ///  <param name="n">The stage.</param>
        ///  <param name="bd">The coefficients of the polynomial that is integrated: see polyfunc.</param>
        ///  <param name="ip">The number of coefficients.</param>
        ///  <returns>The trapezoidal rule with 2^(n - 1) panels.</returns>
        ///  <remarks>
        ///  Stage 1 is the rule with one panel.  Each later stage halves the panels: it adds the values at the new points, of which there are
        ///  2^(n - 2), to the result of the stage before, so that no value is worked out twice.  The stages have to be called in order.
        ///  </remarks>
        public static double trapzd(double a, double b, double s, int n, double[] bd, int ip)
        {
            if (n == 1)
                return 0.5 * (b - a) * (polyfunc(a, bd, ip) + polyfunc(b, bd, ip));

            int it = 1 << (n - 2); //Convert.ToInt32(Math.Pow(2, n - 2));
            double tnm = it;
            double del = (b - a) / tnm;
            double x = a + 0.5 * del;
            double sum = 0.0;
            for (int j = 1; j <= it; j++)
            {
                sum += polyfunc(x, bd, ip);
                x += del;
            }
            return 0.5 * (s + (b - a) * sum / tnm);
        }

        ///  <summary>
        ///  expand a polynomial
        ///  </summary>
        ///  <param name="x">Where the polynomial is wanted.</param>
        ///  <param name="bd">The coefficients: bd[1] is the constant, and bd[j] goes with x^(j - 1).</param>
        ///  <param name="ip">The number of coefficients, which is one more than the degree.</param>
        ///  <returns>bd[1] + bd[2] x + ... + bd[ip] x^(ip - 1).</returns>
        public static double polyfunc(double x, double[] bd, int ip)
        {
            double pf;
            if (x == 0.0)
            {
                pf = bd[1];
            }
            else
            {
                pf = bd[1] + bd[2] * x;
                if (ip > 2)
                {
                    for (int j = 3; j <= ip; j++)
                    {
                        pf += bd[j] * Math.Pow(x, Convert.ToDouble(j - 1));
                    }
                }
            }
            return pf;
        }


        ///  <summary>
        ///  numerical recipies p103
        ///  </summary>
        ///  <param name="xa">The x of the points.</param>
        ///  <param name="ya">The y of the points.</param>
        /// <param name="startIndex">The element of xa and ya at which the points start.</param>
        /// <param name="n">The number of points, 10 at most.</param>
        ///  <param name="x">Where the polynomial through the points is wanted.</param>
        ///  <param name="y">On return, its value there.</param>
        ///  <param name="dy">On return, the last correction that was made to y, which serves as an estimate of the error.</param>
        ///  <param name="ifault">Set to 1 if two of the points have the same x.</param>
        ///  <remarks>
        ///  The polynomial of degree n - 1 through the n points, by Neville's method.  The value starts at the y of the point nearest to x.  c and d
        ///  hold the corrections that bring in one more point at a time, on the one side and on the other, and at each step the correction is taken
        ///  from the side that keeps the points used about x as evenly as can be.
        ///  The area under a fitted polynomial is found with this: the results of the last five stages of trapzd are taken as points, with the
        ///  square of the width of a panel as x, and the polynomial through them is read at a width of zero.
        ///  </remarks>
        public static void polint(double[] xa, double[] ya, int startIndex, int n, double x, out double y, ref double dy, ref int ifault)
        {
            const int nmax = 10;
            double[] c = new double[nmax + 1];
            double[] d = new double[nmax + 1];
            int ns = 1;
            double dif = Math.Abs(x - xa[startIndex]);
            for (int i = 1; i <= n; i++)
            {
                double dift = Math.Abs(x - xa[i + startIndex - 1]);
                if (dift < dif)
                {
                    ns = i;
                    dif = dift;
                }
                c[i] = ya[i + startIndex - 1];
                d[i] = c[i];
            }
            y = ya[ns + startIndex - 1];
            ns -= 1;
            for (int m = 1; m < n; m++)
            {
                for (int i = 1; i <= n - m; i++)
                {
                    double ho = xa[i + startIndex - 1] - x;
                    double hp = xa[i + m + startIndex - 1] - x;
                    double w = c[i + 1] - d[i];
                    double den = ho - hp;
                    if (den == 0.0)
                    {
                        ifault = 1;
                        return;
                    }
                    den = w / den;
                    d[i] = hp * den;
                    c[i] = ho * den;
                }
                if (2 * ns < n - m)
                {
                    dy = c[ns + 1];
                }
                else
                {
                    dy = d[ns];
                    --ns;
                }
                y += dy;
            }
        }

        ///  <summary>
        ///  Poisson GLM by t QR SVD method for iterative least squares solution
        ///  Errors: Poisson
        ///  Link function: log
        ///  </summary>
        /// <param name="selectX">If the nth element is true, include the nth predictor in the regression; if false, exclude it.</param>
        /// <param name="dropped">Message if any are dropped; unchanged if none are. Passed by ref as callers can make multiple calls in sequence then check for drops.</param>
        /// <param name="errMsg">Message if any errors; unchanged if none. Passed by ref as callers can make multiple calls in sequence then check for errors.</param>
        /// <remarks>
        /// The model is log(mu) = X b + offset, where mu is the expected count.  This routine checks the data, sets the starting values, calls the fitting
        /// loop (XIterativeWeightedLeastSquares, where the method is described) and then works out what is reported from what the loop leaves behind:
        ///     df                  the observations used, less the rank of the model
        ///     leverage            the diagonal of the hat matrix, h = w x'(X'WX)^-1 x, where w is the weight that the fit gave the observation
        ///     devianceResidual    the square root of each observation's share of the deviance, with the sign of y - mu
        ///     covariance          (X'WX)^-1 for the coefficients, as the lower triangle packed by rows: element (i, k), k no more than i, is at i(i - 1)/2 + k
        ///     seBeta              the square roots of its diagonal, which is at i(i + 1)/2
        /// The coefficients in beta are the constant, if there is one, and then the selected predictors in order.
        /// Arrays are 1-based: element 0 is unused.  An observation with a prior weight of zero is left out.  The loop is called a second time if the first
        /// call dropped observations at the boundary, so that the fit is made without them from the start.
        /// </remarks>
        public static void X_Poisson_Regression(bool useIntercept, bool useOffset, ref bool useWeights, int records, double[,] x, int predictors, bool[] selectX, int parameters, double[] y, double[] t, double[] weight, ref double deviance, ref int df, double[] beta, ref int rank, double[] seBeta, double[] covariance, double accuracy, int maxIterations, double[] fits, double[] devianceResidual, double[] leverage, double[] offset, out int errLevel, ref string dropped, ref string errMsg)
        {
            double ti = 0;

            double[] eta = new double[records + 1];
            double[,] decomposition = new double[records + 1, records + 1];
            double[] vstd = new double[records + 1];
            double[] wwt = new double[records + 1];
            double[] tmp = new double[records * 2 + 1];

            maxIterations = maxIterations == 0 ? 10 : Math.Abs(maxIterations);
            accuracy = accuracy < Constant.EPSNEG ? Constant.EPSNEG * 20.0 : Math.Abs(accuracy);

            //err_level 0 no errors
            //err_level 1 input data errors
            //err_level 2 calculation errors critical
            //err_level 3 calculation errors carry on

            errLevel = 0;
            if (records < 2)
            {
                errLevel = 1;
                errMsg = "insufficient observations";
                return;
            }
            if (predictors < 1 || parameters < 1)
            {
                errLevel = 1;
                errMsg = "insufficient predictors";
                return;
            }

            int i;
            int observations;
            if (useWeights)
            {
                observations = 0;
                for (i = 1; i <= records; i++)
                {
                    if (weight[i] < 0.0)
                    {
                        errLevel = 1;
                        errMsg = "negative weights";
                        return;
                    }
                    if (weight[i] > 0.0)
                    {
                        observations++;
                    }
                }
            }
            else
            {
                observations = records;
            }
            int count = 0;
            for (i = 1; i <= predictors; i++)
                if (selectX[i])
                    count++;
            if (useIntercept)
                count += 1;
            if (parameters != count)
            {
                errLevel = 1;
                errMsg = "misspecified predictors";
                return;
            }
            if (parameters > observations)
            {
                errLevel = 1;
                errMsg = "more predictors than observations";
                return;
            }
            if (useWeights)
            {
                for (i = 1; i <= records; i++)
                {
                    if (weight[i] > 0.0)
                    {
                        if (y[i] < 0.0)
                        {
                            errLevel = 1;
                            errMsg = "misspecified response variable";
                            return;
                        }
                    }
                }
            }
            else
            {
                for (i = 1; i <= records; i++)
                {
                    if (y[i] < 0.0)
                    {
                        errLevel = 1;
                        errMsg = "misspecified response variable";
                        return;
                    }
                }
            }
            if (!useOffset)
                for (i = 1; i <= records; i++)
                    offset[i] = 0.0;
            // get starting values for linear predictor (eta) and fitted values (fvl)
            X_Poisson_Starting_Values(records, y, fits, eta, weight, observations);
            // iteratively re-weighted least squares by SVD
            XIterativeWeightedLeastSquares(2, useIntercept, ref useWeights, records, x, predictors, selectX, y, t, weight, ref observations, ref deviance, out rank, beta, parameters, fits, eta, vstd, wwt, offset, decomposition, accuracy, maxIterations, out int iter, tmp, ref errLevel, ref dropped, ref errMsg);
            // IEB July 2009: Call again if boundaries hit so that completely determined observations have zero weight   
            if (dropped.Length > 0)
                XIterativeWeightedLeastSquares(2, useIntercept, ref useWeights, records, x, predictors, selectX, y, t, weight, ref observations, ref deviance, out rank, beta, parameters, fits, eta, vstd, wwt, offset, decomposition, accuracy, maxIterations, out iter, tmp, ref errLevel, ref dropped, ref errMsg);
            if (errLevel == 2)
                return;
            df = observations - rank;
            if (df <= 0)
            {
                errLevel = 3;
            }
            else
            {
                // get leverages from matrix of derivatives
                LeverageFromDerivative(useIntercept, records, predictors, x, selectX, parameters, decomposition, rank, wwt, leverage, tmp);
            }
            if (useWeights == false)
            {
                for (i = 1; i <= records; i++)
                {
                    devianceResidual[i] = Math.Sqrt(PoissonDeviance(fits[i], y[i], ref ti));
                    if (y[i] < fits[i] || y[i] == 0.0)
                        devianceResidual[i] = -devianceResidual[i];
                }
            }
            else
            {
                for (i = 1; i <= records; i++)
                {
                    if (weight[i] > 0.0)
                    {
                        devianceResidual[i] = Math.Sqrt(weight[i] * PoissonDeviance(fits[i], y[i], ref ti));
                        if (y[i] < fits[i] || y[i] == 0.0)
                            devianceResidual[i] = -devianceResidual[i];
                    }
                    else
                    {
                        devianceResidual[i] = 0.0;
                    }
                }
            }
            // get variance-covariance matrix from SVD
            X_Covariance_From_SVD(parameters, rank, decomposition, covariance, tmp);
            for (i = 1; i <= parameters; i++)
                seBeta[i] = covariance[(int)Math.Floor((i * i + i) / 2.0)] > 0.0 ? Math.Sqrt(covariance[(int)Math.Floor((i * i + i) / 2.0)]) : 0.0;
        }

        ///  <summary>
        ///  Logistic GLM by QR SVD method for iterative least squares solution
        ///  Errors: binomial
        ///  Link function: logistic
        ///  Residuals: deviance
        ///  </summary>
        ///  <param name="selectX">If the nth element is true, include the nth predictor in the regression; if false, exclude it.</param>
        ///  <param name="err_level">0 = no errors; 1 = input data errors; 2 = calculation errors critical; 3 = calculation errors carry on</param>
        ///  <remarks>
        ///  The model is log(p / (1 - p)) = X b + offset, where p is the probability of a response: y_r responses out of y_t, so that mu = y_t p is the
        ///  expected number.  This routine checks the data, sets the starting values, calls the fitting loop (XIterativeWeightedLeastSquares, where the
        ///  method is described) and then works out what is reported from what the loop leaves behind:
        ///      df            the observations used, less the rank of the model
        ///      leverage      the diagonal of the hat matrix, h = w x'(X'WX)^-1 x, where w is the weight that the fit gave the observation
        ///      residual      the square root of each observation's share of the deviance, with the sign of y_r - mu
        ///      covariance    (X'WX)^-1 for the coefficients, as the lower triangle packed by rows: element (i, k), k no more than i, is at i(i - 1)/2 + k
        ///      se_beta       the square roots of its diagonal, which is at i(i + 1)/2
        ///  The coefficients in beta are the constant, if there is one, and then the selected predictors in order.
        ///  Arrays are 1-based: element 0 is unused.  An observation with a prior weight of zero, or with y_t of zero, is left out.  The loop is called a
        ///  second time if the first call dropped observations at the boundary, so that the fit is made without them from the start.
        ///  </remarks>
        public static void X_Logistic_Regression(bool useIntercept, bool useOffset, ref bool useWeights, int records, double[,] x, int xVariables, bool[] selectX, int parameters, double[] y_r, double[] y_t, double[] weight, out double deviance, ref int df, double[] beta, ref int rank, double[] se_beta, double[] covariance, double accuracy, int max_iterations, double[] fit, double[] residual, double[] leverage, double[] offset, out int err_level, ref string dropped, ref string err_msg)
        {
            int observations = 0;

            double[] eta = new double[records + 1];
            double[,] decomposition = new double[records + 1, records + 1];
            double[] vstd = new double[records + 1];
            double[] wwt = new double[records + 1];
            double[] tmp = new double[records * 2 + 1];
            max_iterations = max_iterations == 0 ? 20 : Math.Abs(max_iterations);
            accuracy = accuracy < Constant.EPSNEG ? Constant.EPSNEG * 10.0 : Math.Abs(accuracy);

            err_level = 0;
            deviance = Constant.MISSING;
            if (records < 2)
            {
                err_level = 1;
                err_msg = "insufficient observations";
                return;
            }
            if (xVariables < 1 || parameters < 1)
            {
                err_level = 1;
                err_msg = "insufficient predictors";
                return;
            }

            if (useWeights)
            {
                observations = 0;
                for (int i = 1; i <= records; i++)
                {
                    if (weight[i] < 0.0)
                    {
                        err_level = 1;
                        err_msg = "negative weights";
                        return;
                    }
                    if (weight[i] > 0.0 && y_t[i] > 0.0)
                        observations++;
                }
            }
            int count = 0;
            for (int i = 1; i <= xVariables; i++)
            {
                if (selectX[i])
                    count++;
            }
            if (useIntercept)
                count++;

            if (useWeights)
            {
                for (int i = 1; i <= records; i++)
                {
                    if (weight[i] > 0.0 && y_t[i] < 0.0)
                    {
                        err_level = 1;
                        err_msg = "invalid response denominator";
                        return;
                    }
                }
            }
            else
            {
                observations = 0;
                for (int i = 1; i <= records; i++)
                {
                    if (y_t[i] < 0.0)
                    {
                        err_level = 1;
                        err_msg = "invalid response denominator";
                        return;
                    }
                    if (y_t[i] > 0.0)
                        observations++;
                }
            }
            if (parameters != count)
            {
                err_level = 1;
                err_msg = "invalid number of available predictors";
                return;
            }
            if (parameters > observations)
            {
                err_level = 1;
                err_msg = "more predictors than observations";
                return;
            }
            if (useWeights)
            {
                for (int i = 1; i <= records; i++)
                {
                    if (weight[i] > 0.0)
                    {
                        if (y_r[i] < 0.0 || y_r[i] > y_t[i])
                        {
                            err_level = 1;
                            err_msg = "invalid response variable, check for r > n";
                            return;
                        }
                    }
                }
            }
            else
            {
                for (int i = 1; i <= records; i++)
                {
                    if (y_r[i] < 0.0 || y_r[i] > y_t[i])
                    {
                        err_level = 1;
                        err_msg = "invalid response variable, check for r > n";
                        return;
                    }
                }
            }
            if (useOffset == false)
            {
                for (int i = 1; i <= records; i++)
                    offset[i] = 0.0;
            }

            // get starting values for linear predictor and fitted values
            X_Logistic_Starting_Values(records, y_r, y_t, fit, eta, weight, observations);

            // iteratively re-weighted least squares by SVD
            XIterativeWeightedLeastSquares(1, useIntercept, ref useWeights, records, x, xVariables, selectX, y_r, y_t, weight, ref observations, ref deviance, out rank, beta, parameters, fit, eta, vstd, wwt, offset, decomposition, accuracy, max_iterations, out int iter, tmp, ref err_level, ref dropped, ref err_msg);
            // IEB July 2009: Call again if boundaries hit so that completely determined observations have zero weight   
            if (dropped.Length > 0)
                XIterativeWeightedLeastSquares(1, useIntercept, ref useWeights, records, x, xVariables, selectX, y_r, y_t, weight, ref observations, ref deviance, out rank, beta, parameters, fit, eta, vstd, wwt, offset, decomposition, accuracy, max_iterations, out iter, tmp, ref err_level, ref dropped, ref err_msg);
            if (err_level == 2)
                return;

            df = observations - rank;
            if (df <= 0)
            {
                err_level = 3;
            }
            else
            {
                // get leverages from matrix of derivatives
                LeverageFromDerivative(useIntercept, records, xVariables, x, selectX, parameters, decomposition, rank, wwt, leverage, tmp);
            }
            if (useWeights == false)
            {
                for (int i = 1; i <= records; i++)
                {
                    if (y_t[i] > 0.0)
                    {
                        residual[i] = Math.Sqrt(LogisticDeviance(fit[i], y_r[i], y_t[i]));
                        if (y_r[i] < fit[i] || y_r[i] == 0.0)
                            residual[i] = -residual[i];
                    }
                    else
                    {
                        residual[i] = 0.0;
                    }
                }
            }
            else
            {
                for (int i = 1; i <= records; i++)
                {
                    if (weight[i] > 0.0)
                    {
                        if (y_t[i] > 0.0)
                        {
                            residual[i] = Math.Sqrt(weight[i] * LogisticDeviance(fit[i], y_r[i], y_t[i]));
                            if (y_r[i] < fit[i] | y_r[i] == 0.0)
                                residual[i] = -residual[i];
                        }
                        else
                        {
                            residual[i] = 0.0;
                        }
                    }
                    else
                    {
                        residual[i] = 0.0;
                    }
                }
            }
            // get variance-covariance matrix from SVD
            X_Covariance_From_SVD(parameters, rank, decomposition, covariance, tmp);
            for (int i = 1; i <= parameters; i++)
            {
                int idx = (int)Math.Floor((i * i + i) / 2.0);
                se_beta[i] = covariance[idx] > 0.0 ? Math.Sqrt(covariance[idx]) : 0.0;
            }
        }

        ///  <summary>
        ///  Get starting values for linear predictor and fitted values for Poisson regression
        ///  </summary>
        ///  <remarks>
        ///  mu starts at y, or at 1 where y is zero, and eta at log(mu).  An observation of zero weight gets zeros, which the fit does not use.
        ///  </remarks>
        private static void X_Poisson_Starting_Values(int n, double[] y, double[] fitted_values, double[] linear_predictor, double[] weight, int observations)
        {
            int i;
            if (n == observations)
            {
                for (i = 1; i <= n; i++)
                {
                    if (y[i] > 0.0)
                    {
                        fitted_values[i] = y[i];
                        linear_predictor[i] = Math.Log(y[i]);
                    }
                    else
                    {
                        fitted_values[i] = 1.0;
                        linear_predictor[i] = 0.0;
                    }
                }
            }
            else
            {
                for (i = 1; i <= n; i++)
                {
                    if (weight[i] > 0.0)
                    {
                        if (y[i] > 0.0)
                        {
                            fitted_values[i] = y[i];
                            linear_predictor[i] = Math.Log(y[i]);
                        }
                        else
                        {
                            fitted_values[i] = 1.0;
                            linear_predictor[i] = 0.0;
                        }
                    }
                    else
                    {
                        linear_predictor[i] = 0.0;
                        fitted_values[i] = 0.0;
                    }
                }
            }
        }

        ///  <summary>
        ///  get starting values for linear predictor (eta) and fitted values (fvl) for logistic regression
        ///  </summary>
        ///  <remarks>
        ///  With r responses out of n, mu starts at n (r + 1/2) / (n + 1), which is strictly between 0 and n even when r is 0 or n, and eta at
        ///  log(mu / (n - mu)).  An observation of zero weight, or with n of zero, gets zeros, which the fit does not use.
        ///  </remarks>
        private static void X_Logistic_Starting_Values(int records, double[] y_r, double[] y_t, double[] fitted_value, double[] linearPredictor, double[] weight, int observations)
        {
            if (records == observations)
            {
                for (int i = 1; i <= records; i++)
                {
                    fitted_value[i] = y_t[i] * (y_r[i] + 0.5) / (y_t[i] + 1.0);
                    linearPredictor[i] = Math.Log(fitted_value[i] / (y_t[i] - fitted_value[i]));
                }
            }
            else
            {
                for (int i = 1; i <= records; i++)
                {
                    if (weight[i] == 0.0 || y_t[i] == 0.0)
                    {
                        fitted_value[i] = 0.0;
                        linearPredictor[i] = 0.0;
                    }
                    else
                    {
                        fitted_value[i] = y_t[i] * (y_r[i] + 0.5) / (y_t[i] + 1.0);
                        linearPredictor[i] = Math.Log(fitted_value[i] / (y_t[i] - fitted_value[i]));
                    }
                }
            }
        }

        /// <summary>
        /// Fits a generalised linear model by iteratively reweighted least squares: model 1 is binomial with a logit link, model 2 is Poisson with a log link.
        /// </summary>
        /// <remarks>
        /// Each pass solves the weighted least squares problem  min || W^(1/2) (z - X b) ||  where, at the current fit,
        ///     z = (eta - offset) + (y - mu) / (dmu/deta)     the working response
        ///     W = (dmu/deta)^2 / var(y)                      the working weight: n p (1 - p) for the logistic model and mu for the Poisson
        /// then sets eta = X b + offset, and mu from eta.  It stops when the deviance changes by less than accuracy * (1 + deviance).
        ///
        /// The equations are solved without forming X'WX, whose condition number is the square of that of W^(1/2) X:
        ///     W^(1/2) X = Q R        by Householder reflections (QRFactorization)
        ///     R = U S P'             the singular value decomposition of the triangle (X_SVD_Regression_Main)
        ///     b = P S^-1 U' Q' W^(1/2) z
        /// Singular values that are negligible beside the largest are left out of the sum, so a model that is not of full rank still gets an answer, and
        /// its rank is returned.
        ///
        /// When the fit has converged one more pass is made, with final set, in which nothing is updated: it leaves the decomposition at the final weights.
        /// On return decomposition holds S^-1 P' in its first rank rows, from which the caller gets the covariance matrix P S^-2 P' = (X'WX)^-1 and the
        /// leverages, and working_weight holds W^(1/2), times the square root of the prior weight if there is one.
        ///
        /// Arrays are 1-based (element 0 is unused), and several are used for more than one thing:
        ///     fit               mu on entry and on return; within a pass, W^(1/2) z and then U'Q'W^(1/2) z
        ///     working_weight    dmu/deta, then W^(1/2)
        ///     variance_std      1 / sd(y): not a variance
        ///     s_diagonals       the scalars of the Householder reflections, then the singular values, then their reciprocals
        ///     decomposition     X, then W^(1/2) X, then its QR factors, then P', then S^-1 P'
        ///     t                 the binomial denominators; for the Poisson model only a flag, set to -1 by PoissonDeviance when a fitted value is not positive
        ///
        /// err_level 2 stops the fit with the message in err_msg; err_level 3 is a warning, and the fit carries on.
        /// </remarks>
        private static void XIterativeWeightedLeastSquares(int model, bool useIntercept, ref bool useWeights, int records, double[,] x, int predictors, bool[] select_x, double[] y, double[] t, double[] weight, ref int observations, ref double deviance, out int rank, double[] beta, int parameters, double[] fit, double[] eta, double[] variance_std, double[] working_weight, double[] offset, double[,] decomposition, double accuracy, int max_iterations, out int iterations, double[] s_diagonals, ref int err_level, ref string dropped, ref string err_msg)
        {
            int i; int k;
            int j; int rank1 = 0;
            double dev1 = 0;

            //level 2 errors terminate with the message in err_msg
            //level 3 errors are allowed to continue and generate a warning in err_msg

            rank = parameters;
            // final is set for the last pass, which updates nothing.  indqy is passed to the singular value decomposition: 1 if the right-hand side is to be
            // transformed with the matrix, 0 on the final pass, when there is none.
            bool final = false;
            int indqy = 1;
            iterations = 0;
            // setup x matrix
            // The design matrix goes into decomposition: a column of ones for the constant, then the selected predictors in order.  (With a constant, every
            // column up to the number of predictors is set to one first, and the selected predictors then overwrite from the second.)
            if (useIntercept)
            {
                for (i = 1; i <= records; i++)
                {
                    for (k = 1; k <= predictors; k++)
                    {
                        decomposition[i, k] = 1.0;
                    }
                }
                k = 1;
            }
            else
            {
                k = 0;
            }
            for (j = 1; j <= predictors; j++)
            {
                if (select_x[j])
                {
                    k++;
                    for (i = 1; i <= records; i++)
                        decomposition[i, k] = x[i, j];
                }
            }
            // working weights and response
            // One pass of this loop is one weighted least squares fit
            do
            {
                iterations++;
                // get derivative then variance
                // dmu/deta goes into working_weight and 1 / sd(y) into variance_std, both at the current fit
                switch (model)
                {
                    case 1:
                        X_Binomial_Derivative(records, eta, t, working_weight, weight, observations);
                        X_Binomial_Variance(records, fit, t, variance_std, weight, observations);
                        break;
                    case 2:
                        X_Poisson_Derivative(records, eta, working_weight, weight, observations);
                        X_Poisson_Variance(records, fit, variance_std, weight, observations);
                        break;
                }

                // The right-hand side W^(1/2) z, written over the fitted values.  Since W^(1/2) = (dmu/deta) / sd,
                //     W^(1/2) z = ((eta - offset) dmu/deta + (y - mu)) / sd
                // Not on the final pass, which keeps the fitted values for the caller.
                if (final == false)
                {
                    for (i = 1; i <= records; i++)
                    {
                        fit[i] = ((eta[i] - offset[i]) * working_weight[i] + y[i] - fit[i]) * variance_std[i];
                    }
                }
                // W^(1/2) = (dmu/deta) / sd
                for (i = 1; i <= records; i++)
                {
                    working_weight[i] = variance_std[i] * working_weight[i];
                }
                // Prior weights: both sides of each equation are multiplied by the square root of the observation's weight
                if (useWeights)
                {
                    if (final)
                    {
                        for (i = 1; i <= records; i++)
                        {
                            if (weight[i] > 0.0)
                            {
                                working_weight[i] = working_weight[i] * Math.Sqrt(weight[i]);
                            }
                        }
                    }
                    else
                    {
                        for (i = 1; i <= records; i++)
                        {
                            if (weight[i] > 0.0)
                            {
                                double sqwt = Math.Sqrt(weight[i]);
                                working_weight[i] = working_weight[i] * sqwt;
                                fit[i] = fit[i] * sqwt;
                            }
                        }
                    }
                }
                // W^(1/2) X: each row of the design matrix multiplied by its weight
                for (i = 1; i <= parameters; i++)
                {
                    if (records > 0)
                    {
                        for (j = 1; j <= records; j++)
                        {
                            decomposition[j, i] = working_weight[j] * decomposition[j, i];
                        }
                    }
                }
                // W^(1/2) X = Q R.  R replaces the upper triangle, the vectors of the reflections go below it and their scalars into s_diagonals.
                QRFactorization(records, parameters, decomposition, s_diagonals);
                // Q' applied to the right-hand side: only its first elements, as many as there are parameters, are needed from here on
                if (final == false)
                {
                    X_Householder_QR_Transformation(records, parameters, decomposition, s_diagonals, fit);
                }

                // R = U S P'.  The singular values replace s_diagonals, P' replaces the top of decomposition, and U' is applied to the right-hand side
                // unless indqy is 0, as it is on the final pass.
                X_SVD_Regression_Main(parameters, decomposition, records, indqy, fit, s_diagonals, ref err_level, ref err_msg);
                if (err_level == 0)
                {
                    // The rank is the number of singular values that are not negligible.  If it changes from one pass to the next the fit is not stable.
                    rank = IsRank(parameters, s_diagonals);
                    if (iterations == 1)
                    {
                        rank1 = rank;
                    }
                    else
                    {
                        if (rank != rank1)
                        {
                            err_msg = "Rank changed, consider dropping predictors";
                            err_level = 3;
                        }
                    }
                    // S^-1 P': each of the first rank rows of P' is divided by its singular value
                    for (i = 1; i <= rank; i++)
                        s_diagonals[i] = 1.0 / s_diagonals[i];

                    for (i = 1; i <= parameters; i++)
                    {
                        if (records > 0)
                        {
                            for (j = 1; j <= rank; j++)
                                decomposition[j, i] = s_diagonals[j] * decomposition[j, i];
                        }
                    }
                }
                // The final pass ends here, with S^-1 P' in decomposition for the covariance matrix and the leverages
                if (final)
                    return;

                // b = (S^-1 P')' c, where c is the right-hand side after both transformations: the least squares solution, and the shortest one if the model
                // is not of full rank
                for (i = 1; i <= parameters; i++)
                    beta[i] = 0.0;
                for (j = 1; j <= rank; j++)
                {
                    if (fit[j] != 0.0)
                    {
                        for (i = 1; i <= parameters; i++)
                            beta[i] += fit[j] * decomposition[j, i];
                    }
                }
                // The design matrix again, since the decomposition was made in its place, for the linear predictor eta = X b + offset
                if (useIntercept)
                {
                    k = 1;
                    for (i = 1; i <= parameters; i++)
                    {
                        if (records > 0)
                        {
                            for (j = 1; j <= records; j++)
                                decomposition[j, i] = 1.0;
                        }
                    }
                }
                else
                {
                    k = 0;
                }
                for (j = 1; j <= predictors; j++)
                {
                    if (select_x[j])
                    {
                        k++;
                        for (i = 1; i <= records; i++)
                            decomposition[i, k] = x[i, j];
                    }
                }
                if (records == observations)
                {
                    for (i = 1; i <= records; i++)
                        eta[i] = X_SUMPROD(parameters, beta, decomposition, i) + offset[i];
                }
                else
                {
                    for (i = 1; i <= records; i++)
                    {
                        if (weight[i] == 0.0)
                            eta[i] = 0.0;
                        else
                            eta[i] = X_SUMPROD(parameters, beta, decomposition, i) + offset[i];
                    }
                }
                //  fit response from linear predictor
                // mu from eta, by the inverse of the link.  An observation whose eta is too large to tell from the boundary is dropped, by setting its weight
                // to zero, and named in dropped.
                switch (model)
                {
                    case 1:
                        X_Logistic_Response(records, eta, fit, t, weight, ref observations, ref useWeights, ref dropped);
                        break;
                    case 2:
                        X_Log_Poisson_Response(records, eta, fit, weight, ref observations, ref useWeights, ref dropped);
                        break;
                }

                // The deviance at the new fit.  PoissonDeviance sets t to -1 if a fitted value is not positive, which stops the fit; LogisticDeviance leaves t as it is.
                deviance = 0.0;
                if (useWeights)
                {
                    //  calculate deviance
                    switch (model)
                    {
                        case 1:
                            for (i = 1; i <= records; i++)
                            {
                                if (weight[i] > 0.0)
                                {
                                    deviance += weight[i] * LogisticDeviance(fit[i], y[i], t[i]);
                                    if (t[i] < 0.0)
                                    {
                                        err_level = 2;
                                        err_msg = "Boundary hit, try dropping predictors";
                                        return;
                                    }
                                }
                            }
                            break;
                        case 2:
                            for (i = 1; i <= records; i++)
                            {
                                if (weight[i] > 0.0)
                                {
                                    deviance += weight[i] * PoissonDeviance(fit[i], y[i], ref t[i]);
                                    if (t[i] < 0.0)
                                    {
                                        err_level = 2;
                                        err_msg = "Boundary hit, try dropping predictors";
                                        return;
                                    }
                                }
                            }
                            break;
                    }

                }
                else
                {
                    switch (model)
                    {
                        case 1:
                            for (i = 1; i <= records; i++)
                            {
                                deviance += LogisticDeviance(fit[i], y[i], t[i]);
                                if (t[i] < 0.0)
                                {
                                    err_level = 2;
                                    err_msg = "Boundary hit, try dropping predictors";
                                    return;
                                }
                            }
                            break;
                        case 2:
                            for (i = 1; i <= records; i++)
                            {
                                deviance += PoissonDeviance(fit[i], y[i], ref t[i]);
                                if (t[i] < 0.0)
                                {
                                    err_level = 2;
                                    err_msg = "Boundary hit, try dropping predictors";
                                    return;
                                }
                            }
                            break;
                    }

                }
                // Converged when the deviance has changed by less than accuracy * (1 + deviance), or is zero: one more pass follows, with final set
                if (deviance <= 0)
                {
                    final = true;
                    indqy = 0;
                }
                else if (iterations == 1)
                {
                    dev1 = deviance;
                }
                else
                {
                    if (Math.Abs(deviance - dev1) < (1.0 + deviance) * accuracy)
                    {
                        final = true;
                        indqy = 0;
                    }
                    else
                    {
                        dev1 = deviance;
                    }
                }
                // Too many passes: a warning, and the final pass follows all the same
                if (iterations > max_iterations)
                {
                    err_level = 3;
                    err_msg = "Failed to converge in " + max_iterations.ToString() + " iterations";
                    final = true;
                }
            }
            while (true);
        }


        /// <summary>
        /// The leverage of each observation: the diagonal of the hat matrix, h = w x'(X'WX)^-1 x.
        /// </summary>
        /// <remarks>
        /// q holds S^-1 P' as the fitting loop left it, so that (X'WX)^-1 = (S^-1 P')'(S^-1 P') and h is w times the squared length of the vector S^-1 P' x.
        /// workingWeight holds the square root of w.  work holds the row x of the design matrix in its first ip elements (a one first, if there is a
        /// constant) and the vector S^-1 P' x after them.
        /// </remarks>
        private static void LeverageFromDerivative(bool mean, int n, int m, double[,] x, bool[] isx, int ip, double[,] q, int rank, double[] workingWeight, double[] h, double[] work)
        {
            int im = mean ? 1 : 0;
            work[1] = 1.0;
            for (int i = 1; i <= n; i++)
            {
                int k = im;
                for (int j = 1; j <= m; j++)
                {
                    if (isx[j])
                    {
                        k++;
                        work[k] = x[i, j];
                    }
                }
                for (int ix = 1; ix <= rank; ix++)
                    work[ip + ix] = 0.0;
                // The ip columns of the model.  (One more was taken, which held ones, not zeros, when a constant was fitted and two or more predictors
                // were left unselected: the leverages of such a fit were wrong.)
                for (int j = 1; j <= ip; j++)
                {
                    double temp = work[j];
                    if (temp != 0.0)
                    {
                        for (int ix = 1; ix <= rank; ix++)
                            work[ip + ix] = work[ip + ix] + temp * q[ix, j];
                    }
                }
                if (rank > 0)
                {
                    h[i] = 0.0;
                    for (int j = ip + 1; j <= rank + ip; j++)
                        h[i] = h[i] + work[j] * work[j];
                }
                h[i] = h[i] * workingWeight[i] * workingWeight[i];
            }
        }

        ///  <summary>
        ///  log Poisson deviance
        ///  </summary>
        ///  <remarks>
        ///  One observation's share of the deviance: 2 (y log(y / mu) - (y - mu)), which is 2 mu when y is zero.  A fitted value that is not positive cannot
        ///  be used: t is set to -1 to tell the caller, and the share is returned as zero.
        ///  </remarks>
        private static double PoissonDeviance(double fit, double y, ref double t)
        {
            double dev = 0.0;
            if (fit <= 0.0)
                t = -1.0;
            else if (y > 0.0)
                dev = y * Math.Log(y / fit) - (y - fit);
            else
                dev = fit;
            if (dev < 0.0)
                dev = 0.0;
            return 2.0 * dev;
        }


        ///  <summary>
        ///  logistic binary deviance
        ///  </summary>
        ///  <remarks>
        ///  One observation's share of the deviance, for y responses out of t with mu expected: 2 (y log(y / mu) + (t - y) log((t - y) / (t - mu))).
        ///  When y is 0 or t one of the two terms vanishes, which leaves 2 t |log(1 - mu / t)| or 2 t |log(mu / t)|.
        ///  </remarks>
        private static double LogisticDeviance(double fit, double y, double t)
        {
            double dev = 0.0;
            //  Hosmer and Lemeshow p 138
            if (y == 0.0)
            {
                dev = t * Math.Abs(Math.Log(1.0 - fit / t));
            }
            else if (y == t)
            {
                dev = t * Math.Abs(Math.Log(fit / t));
            }
            else
            {
                if (y > 0.0 && y < t)
                    dev = y * Math.Log(y / fit) + (t - y) * Math.Log((t - y) / (t - fit));
            }
            if (dev < 0.0)
                dev = 0.0;
            return 2.0 * dev;
        }

        /// <summary>
        /// The covariance matrix of the coefficients, (X'WX)^-1 = P S^-2 P', from q = S^-1 P' as the fitting loop left it.
        /// </summary>
        /// <remarks>
        /// Element (i, k) is the sum, over the first rank rows of q, of q[j, i] q[j, k].  Only the lower triangle is formed, packed by rows: element (i, k),
        /// k no more than i, goes to i(i - 1)/2 + k, so that the diagonal is at i(i + 1)/2.  work holds column i of q.
        /// </remarks>
        private static void X_Covariance_From_SVD(int p, int rank, double[,] q, double[] covariance, double[] work)
        {
            int ij = 1;
            for (int i = 1; i <= p; i++)
            {
                if (rank > 0)
                {
                    for (int iy = 1; iy <= rank; iy++)
                        work[iy] = q[iy, i];
                }
                for (int k = 1; k <= i; k++)
                    covariance[ij - 1 + k] = 0.0;
                for (int j = 1; j <= rank; j++)
                {
                    double temp = work[j];
                    if (temp != 0.0)
                    {
                        for (int k = 1; k <= i; k++)
                            covariance[ij - 1 + k] = covariance[ij - 1 + k] + temp * q[j, k];
                    }
                }
                ij += i;
            }
        }


        ///  <summary>
        ///  Poisson derivative of log link function
        ///  </summary>
        ///  <remarks>dmu/deta = exp(eta), which is mu.  Zero for an observation of zero weight.</remarks>
        private static void X_Poisson_Derivative(int records, double[] eta, double[] derivative, double[] weight, int observations)
        {
            if (records == observations)
            {
                for (int i = 1; i <= records; i++)
                    derivative[i] = Math.Exp(eta[i]);
            }
            else
            {
                for (int i = 1; i <= records; i++)
                    derivative[i] = weight[i] > 0.0 ? Math.Exp(eta[i]) : 0.0;
            }
        }

        ///  <summary>
        ///  binomial logistic derivative
        ///  </summary>
        ///  <remarks>dmu/deta = t e / (1 + e)^2 with e = exp(eta), which is t p (1 - p).  Zero for an observation of zero weight or with t of zero.</remarks>
        private static void X_Binomial_Derivative(int records, double[] eta, double[] t, double[] derivative, double[] weight, int observations)
        {
            int i;
            double e;

            if (records == observations)
            {
                for (i = 1; i <= records; i++)
                {
                    e = Math.Exp(eta[i]);
                    derivative[i] = t[i] * e / ((1.0 + e) * (1.0 + e));
                }
            }
            else
            {
                for (i = 1; i <= records; i++)
                {
                    if (weight[i] != 0.0 & t[i] != 0.0)
                    {
                        e = Math.Exp(eta[i]);
                        derivative[i] = t[i] * e / ((1.0 + e) * (1.0 + e));
                    }
                    else
                    {
                        derivative[i] = 0.0;
                    }
                }
            }
        }

        ///  <summary>
        ///  Poisson variance
        ///  </summary>
        ///  <remarks>Not the variance but the reciprocal of the standard deviation of y: 1 / sqrt(mu).  Zero for an observation of zero weight.</remarks>
        private static void X_Poisson_Variance(int records, double[] fit, double[] variance_std, double[] weight, int observations)
        {
            if (records != observations)
            {
                for (int i = 1; i <= records; i++)
                    variance_std[i] = weight[i] == 0.0 ? 0.0 : 1.0 / Math.Sqrt(fit[i]);
            }
            else
            {
                for (int i = 1; i <= records; i++)
                    variance_std[i] = 1.0 / Math.Sqrt(fit[i]);
            }
        }

        ///  <summary>
        ///  binomial variance
        ///  </summary>
        ///  <remarks>
        ///  Not the variance but the reciprocal of the standard deviation of y: sqrt(t / (mu (t - mu))), which is 1 / sqrt(t p (1 - p)).  Zero for an
        ///  observation of zero weight or with t of zero.
        ///  </remarks>
        private static void X_Binomial_Variance(int records, double[] fit, double[] t, double[] variance_std, double[] weight, int observations)
        {
            if (records != observations)
            {
                for (int i = 1; i <= records; i++)
                    variance_std[i] = weight[i] == 0.0 || t[i] == 0.0 ? 0.0 : Math.Sqrt(t[i] / (fit[i] * (t[i] - fit[i])));
            }
            else
            {
                for (int i = 1; i <= records; i++)
                    variance_std[i] = Math.Sqrt(t[i] / (fit[i] * (t[i] - fit[i])));
            }
        }

        ///  <summary>
        ///  This routine finds the QR factorization of matrix A (m by n, where m>=n) such that the matrix is reduced to upper triangular form by orthogonal transformations.
        ///  Householder reduction method.
        ///  </summary>
        ///  <remarks>
        ///  On return the upper triangle of a holds R.  Each column below the diagonal holds the vector of the reflection that cleared it, and zeta holds one
        ///  scalar for each reflection, or zero where none was needed.  Reflection i is H = I - u u', where u is zero above element i, zeta[i] at element i
        ///  and the stored column below it, scaled so that u'u = 2.  Q' is the product of the reflections, the first applied first.
        ///  This is the factorisation that LAPACK calls DGEQRF, which holds each reflection as I - tau v v' with the first element of v equal to one.
        ///  </remarks>
        private static void QRFactorization(int m, int n, double[,] a, double[] zeta)
        {
            for (int i1 = 1; i1 <= Math.Min(m - 1, n); i1++)
            {
                // The reflection that clears column i1 below the diagonal
                X_Setup_Householder_Reflection(m - i1, ref a[i1, i1], a, out zeta[i1], i1 + 1, i1, m);
                if (zeta[i1] > 0.0 & i1 < n)
                {
                    // The reflection applied to the columns to the right.  With zeta put on the diagonal for the moment, column i1 from the diagonal down
                    // is the vector u.  The rest of zeta is borrowed for the products u'a of u with each column a, and each column then becomes a - (u'a) u.
                    double temp_1 = a[i1, i1];
                    a[i1, i1] = zeta[i1];
                    for (int i = 1; i <= n - i1; i++)
                        zeta[i1 + i] = 0.0;
                    int iz1 = i1;
                    int iz2 = i1;
                    for (int i2 = 1; i2 <= m - i1 + 1; i2++)
                    {
                        double temp_2 = a[iz1, iz2];
                        iz1 += 1;
                        if (iz1 > m)
                        {
                            iz1 = 1;
                            iz2 += 1;
                        }
                        if (temp_2 != 0.0)
                        {
                            for (int i3 = 1; i3 <= n - i1; i3++)
                                zeta[i1 + i3] = zeta[i1 + i3] + temp_2 * a[i1 - 1 + i2, i1 + i3];
                        }
                    }
                    for (int i2 = 1; i2 <= n - i1; i2++)
                    {
                        if (zeta[i1 + i2] != 0.0)
                        {
                            iz1 = i1;
                            iz2 = i1;
                            for (int i3 = 1; i3 <= m - i1 + 1; i3++)
                            {
                                a[i1 - 1 + i3, i1 + i2] = a[i1 - 1 + i3, i1 + i2] + a[iz1, iz2] * -zeta[i1 + i2];
                                iz1 += 1;
                                if (iz1 > m)
                                {
                                    iz1 = 1;
                                    iz2 += 1;
                                }
                            }
                        }
                    }
                    a[i1, i1] = temp_1;
                }
            }
            if (m == n)
                zeta[n] = 0.0;
        }

        ///  <summary>
        ///  b := Q'b, where Q is the orthogonal matrix of the factorisation a = QR that QRFactorization left in a and zeta: the reflections are applied to b in
        ///  the order in which they were made.
        ///  </summary>
        ///  <remarks>
        ///  The first n elements of the result are the right-hand side of R x = Q'b, whose solution is the least squares solution; the squares of the rest
        ///  add up to the residual sum of squares.  LAPACK calls this DORMQR, used with the transpose.
        ///  </remarks>
        private static void X_Householder_QR_Transformation(int m, int n, double[,] a, double[] zeta, double[] b)
        {
            for (int i1 = 1; i1 <= n; i1++)
            {
                double zeta_i1 = zeta[i1];
                if (zeta_i1 > 0.0)
                {
                    // With zeta put on the diagonal for the moment, column i1 from the diagonal down is the vector u: hold is u'b, and b becomes b - (u'b) u
                    double temp = a[i1, i1];
                    a[i1, i1] = zeta_i1;
                    double hold = 0.0;
                    int iz1 = i1;
                    int iz2 = i1;
                    for (int i2 = 1; i2 <= m - i1 + 1; i2++)
                    {
                        if (a[iz1, iz2] != 0.0)
                            hold += a[iz1, iz2] * b[i2 + i1 - 1];
                        iz1 += 1;
                        if (iz1 > m)
                        {
                            iz1 = 1;
                            iz2 += 1;
                        }
                    }
                    if (hold != 0.0)
                    {
                        iz1 = i1;
                        for (int i2 = 1; i2 <= m - i1 + 1; i2++)
                        {
                            b[iz1] = b[iz1] + a[i2 + i1 - 1, i1] * -hold;
                            iz1 += 1;
                            if (iz1 > m)
                                iz1 = 1;
                        }
                    }
                    a[i1, i1] = temp;
                }
            }
        }

        ///  <summary>
        ///  Singular value decomposition of a real upper triangular matrix (n by n) factorized as R = Q*S*P'.
        ///  First reduce R to its bidiagonal form using Givens' plane rotations.
        ///  Then use the QR algorithm to get the singular value decomposition of the bidiagonal form.
        ///  </summary>
        ///  <remarks>
        ///  On entry the upper triangle of a holds R; m is the number of rows that the index arithmetic takes the array to have.  On return sv holds the
        ///  singular values, largest first, the first n rows of a hold P' (row j is the right singular vector that goes with sv[j]) and, if ncolb is 1, the
        ///  first n elements of b have been multiplied by Q', which the fitting loop calls U'.
        ///  In LAPACK the same three steps are DGEBRD (reduction to bidiagonal form, there by reflections), DORGBR (forming P') and DBDSQR (the singular
        ///  values of a bidiagonal matrix).
        ///  </remarks>
        private static void X_SVD_Regression_Main(int n, double[,] a, int m, int ncolb, double[] b, double[] sv, ref int err_level, ref string err_msg)
        {
            double[] work = new double[2 * m + 1];
            X_SVD_Bidiagonal_Reduction(n, a, m, sv, work, ncolb, b);
            X_SVD_P_Prime(n, a, m);
            int ncolp = n;
            X_SVD_of_Bidiagonal(n, sv, work, ncolb, b, ncolp, a, m, out int ierr);
            if (ierr != 0)
            {
                err_level = 2;
                err_msg = "SVD failed";
            }
        }

        ///  <summary>
        ///  Factorize upper triangular matrix as R = Q*B*P'.
        ///  </summary>
        ///  <remarks>
        ///  B is upper bidiagonal: its diagonal is returned in diag and the line above the diagonal in super_diag.  Row k of the triangle is cleared beyond
        ///  the element next to the diagonal by rotating neighbouring columns, from the last column inwards.  Each rotation of columns puts an element below
        ///  the diagonal, and a rotation of rows takes it away again (X_SVD_Rotation_Transform).  The rotations of rows make up Q' and are applied to y as
        ///  they are made, if ncoly is 1.  The rotations of columns make up P: the tangent of each is kept in place of the element that it cleared, for
        ///  X_SVD_P_Prime.  The index arithmetic treats a as stored by columns, m rows to a column.
        ///  </remarks>
        private static void X_SVD_Bidiagonal_Reduction(int n, double[,] a, int m, double[] diag, double[] super_diag, int ncoly, double[] y)
        {
            for (int k = 1; k <= n - 2; k++)
            {
                int ix = 1 + (n - k - 2) * m;
                int iax;
                int izb;
                int iz2b;
                for (int i = n - k - 1; i >= 2; i--)
                {
                    iax = ix - m;
                    int iz = iax - iax / m * m + k - 1;
                    int iz2 = iax / m + 2 + k;
                    iax = ix;
                    izb = iax - iax / m * m + k - 1;
                    iz2b = iax / m + 2 + k;
                    X_SVD_Rotation_Angle(ref a[iz, iz2], ref a[izb, iz2b], out super_diag[i + k], out diag[i + k]);
                    ix -= m;
                }
                iax = ix;
                izb = iax - iax / m * m + k - 1;
                iz2b = iax / m + 1 + k + 1;
                X_SVD_Rotation_Angle(ref a[k, k + 1], ref a[izb, iz2b], out super_diag[k + 1], out diag[k + 1]);
                X_SVD_Rotation_Transform(n - k, 1, n - k, super_diag, diag, a, m, k);
                if (ncoly > 0)
                {
                    if (Math.Min(n, k + 1) >= 1 & n > k + 1)
                    {
                        for (int j = n - 1; j >= k + 1; j--)
                        {
                            if (super_diag[j] != 1.0 || diag[j] != 0.0)
                            {
                                double etemp = super_diag[j];
                                double dtemp = diag[j];
                                double temp = y[j + 1];
                                y[j + 1] = etemp * temp - dtemp * y[j];
                                y[j] = dtemp * temp + etemp * y[j];
                            }
                        }
                    }
                }
            }
            for (int k = 1; k < n; k++)
            {
                diag[k] = a[k, k];
                super_diag[k] = a[k, k + 1];
            }
            diag[n] = a[n, n];
        }

        ///  <summary>
        ///  Reduce a real bidiagonal matrix to diagonal form by orthogonal transformations.
        ///  </summary>
        ///  <remarks>
        ///  The QR algorithm with shifts, working on the bidiagonal matrix from the bottom row up.  Rotations made from the left are applied to b as well, if
        ///  ncolb is 1, and those made from the right to the rows of z, if ncolz is not zero: that is how Q'b of the reduction becomes U'b, and P' of the
        ///  reduction becomes P' of the decomposition.  On return diag holds the singular values, none negative and the largest first.  ifail is zero if
        ///  all of them were found within 50 n steps; otherwise it is the number of rows still to be dealt with.  LAPACK calls this DBDSQR.
        ///  </remarks>
        private static void X_SVD_of_Bidiagonal(int n, double[] diag, double[] super_diag, int ncolb, double[] b, int ncolz, double[,] z, int m, out int ifail)
        {
            double[] wrk0 = new double[n + 1];
            double[] wrk1 = new double[n + 1];
            double[] wrk2 = new double[n + 1];
            double[] wrk3 = new double[n + 1];
            wrk0[1] = 0;
            bool wantb = ncolb > 0;
            bool wantz = ncolz > 0;
            // The matrix is scaled by its largest element, so that the tests for negligible elements work on numbers near 1
            double max = Math.Abs(diag[1]);
            for (int i1 = 2; i1 <= n; i1++)
                max = Max3(max, Math.Abs(diag[i1]), Math.Abs(super_diag[i1 - 1]));
            if (max > 0)
            {
                X_SVD_Vector_by_Scalar(n, 1.0 / max, diag);
                X_SVD_Vector_by_Scalar(n - 1, 1.0 / max, super_diag);
            }
            int maxit = 50 * n;
            int iter = 1;
            int i0 = n;
            // Rows 1 to i0 are still to be dealt with: the singular values below them have been found
            while (i0 > 1 && iter <= maxit)
            {
                X_SVD_Test_Bidiagonal_Split(i0, diag, super_diag, out bool force, out int split_row);
                int i3 = split_row + 1;
                double ctemp;
                double stemp;
                // A negligible diagonal element.  In row i0 the column above it is rotated away from the right, and z takes the rotations; in a row further
                // up the rest of the row is rotated away from the left, and b takes them.
                if (force)
                {
                    if (split_row == i0)
                    {
                        X_SVD_Plane_Rotate(i0, diag, super_diag, wantz, wrk2, wrk3);
                        if (wantz)
                        {
                            if (Math.Min(n, ncolz) >= 1 && i0 > 1 && i0 <= n)
                            {
                                for (int i2 = i0 - 1; i2 >= 1; i2--)
                                {
                                    if (wrk2[i2] != 1.0 || wrk3[i2] != 0.0)
                                    {
                                        ctemp = wrk2[i2];
                                        stemp = wrk3[i2];
                                        for (int i1 = 1; i1 <= ncolz; i1++)
                                        {
                                            double temp = z[i2, i1];
                                            z[i2, i1] = stemp * z[i0, i1] + ctemp * temp;
                                            z[i0, i1] = ctemp * z[i0, i1] - stemp * temp;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        if (split_row > 0 && split_row < i0)
                        {
                            int i1 = split_row;
                            double temp = super_diag[i1];
                            super_diag[i1] = 0.0;
                            X_SVD_Rotation_Angle(ref diag[i1 + 1], ref temp, out ctemp, out stemp);
                            if (wantb)
                            {
                                wrk0[i1] = ctemp;
                                wrk1[i1] = -stemp;
                            }
                            for (i1 = split_row + 1; i1 < i0; i1++)
                            {
                                temp = -stemp * super_diag[i1];
                                super_diag[i1] = ctemp * super_diag[i1];
                                X_SVD_Rotation_Angle(ref diag[i1 + 1], ref temp, out ctemp, out stemp);
                                if (wantb)
                                {
                                    wrk0[i1] = ctemp;
                                    wrk1[i1] = -stemp;
                                }
                            }
                        }
                        if (wantb)
                        {
                            if (Math.Min(n, split_row) >= 1 & i0 > split_row & i0 <= n)
                            {
                                for (int i2 = split_row + 1; i2 <= i0; i2++)
                                {
                                    ctemp = wrk0[i2 - 1];
                                    stemp = wrk1[i2 - 1];
                                    if (ctemp != 1.0 || stemp != 0.0)
                                    {
                                        double temp = b[i2];
                                        b[i2] = ctemp * temp - stemp * b[split_row];
                                        b[split_row] = stemp * temp + ctemp * b[split_row];
                                    }
                                }
                            }
                        }
                    }
                }
                // Nothing left above the diagonal in row i0: its singular value has been found.  Otherwise, one QR step on rows i3 to i0.
                if (i3 >= i0)
                {
                    i0 -= 1;
                }
                else
                {
                    double ekm2 = i0 > i3 + 1 ? super_diag[i0 - 2] : 0.0;
                    X_SVD_QR_Shift_Parameters(diag[i3], super_diag[i3], diag[i0 - 1], diag[i0], ekm2, super_diag[i0 - 1], out double cs, out double sn);
                    X_SVD_QR_Rotate(i3, i0, diag, super_diag, cs, sn, wantb, wrk0, wrk1, wantz, wrk2, wrk3);
                    if (wantb)
                    {
                        if (Math.Min(n, i3) >= 1 && i0 > i3 && i0 <= n)
                        {
                            for (int i2 = i3; i2 < i0; i2++)
                            {
                                if (wrk0[i2] != 1.0 || wrk1[i2] != 0.0)
                                {
                                    ctemp = wrk0[i2];
                                    stemp = wrk1[i2];
                                    double temp = b[i2 + 1];
                                    b[i2 + 1] = ctemp * temp - stemp * b[i2];
                                    b[i2] = stemp * temp + ctemp * b[i2];
                                }
                            }
                        }
                    }
                    if (wantz)
                    {
                        if (Min3(n, ncolz, i3) >= 1 || i0 > i3 || i0 <= n)
                        {
                            for (int i2 = i3; i2 < i0; i2++)
                            {
                                if (wrk2[i2] != 1.0 || wrk3[i2] != 0.0)
                                {
                                    ctemp = wrk2[i2];
                                    stemp = wrk3[i2];
                                    for (int i1 = 1; i1 <= ncolz; i1++)
                                    {
                                        double temp = z[i2 + 1, i1];
                                        z[i2 + 1, i1] = ctemp * temp - stemp * z[i2, i1];
                                        z[i2, i1] = stemp * temp + ctemp * z[i2, i1];
                                    }
                                }
                            }
                        }
                    }
                    iter++;
                }
            }
            // The scaling undone
            if (max > 0.0)
            {
                X_SVD_Vector_by_Scalar(n, max, diag);
                X_SVD_Vector_by_Scalar(n - 1, max, super_diag);
            }
            // A singular value is not negative: the sign of one that came out so goes to the matching element of b
            for (int i1 = i0; i1 <= n; i1++)
            {
                if (diag[i1] < 0.0)
                {
                    diag[i1] = -diag[i1];
                    if (wantb)
                    {
                        if (ncolb > 0)
                            b[i1] = -b[i1];
                    }
                }
            }
            // Sorted, the largest first.  The interchanges are recorded in wrk0 (as whole numbers plus a quarter, so that they convert back safely) and are
            // then made in b and in the rows of z.
            for (int i2 = 1; i2 < i0; i2++)
                wrk0[i2] = i2 + 0.25;
            for (int i2 = i0; i2 <= n; i2++)
            {
                double bmax = diag[i2];
                int i3 = i2;
                for (int i1 = i2 + 1; i1 <= n; i1++)
                {
                    if (diag[i1] > bmax)
                    {
                        bmax = diag[i1];
                        i3 = i1;
                    }
                }
                wrk0[i2] = Convert.ToDouble(i3) + 0.25;
                if (i3 > i2)
                {
                    double temp = diag[i2];
                    diag[i2] = diag[i3];
                    diag[i3] = temp;
                }
            }
            if (wantb)
            {
                for (int i1 = 1; i1 <= n; i1++)
                {
                    int i3 = Convert.ToInt32(wrk0[i1]);
                    if (i3 != i1)
                    {
                        double temp = b[i1];
                        b[i1] = b[i3];
                        b[i3] = temp;
                    }
                }
            }
            if (wantz)
            {
                for (int i1 = 1; i1 <= n; i1++)
                {
                    int i3 = Convert.ToInt32(wrk0[i1]);
                    if (i3 != i1)
                    {
                        for (int i2 = 1; i2 <= ncolz; i2++)
                        {
                            double temp = z[i1, i2];
                            z[i1, i2] = z[i3, i2];
                            z[i3, i2] = temp;
                        }
                    }
                }
            }
            // Zero if every singular value was found; otherwise the number of rows still to be dealt with
            wrk0[1] = iter;
            ifail = i0 == 1 ? 0 : i0;
        }


        ///  <summary>
        ///  Return P' from the upper triangular matrix factorized as R = Q*B*P'.
        ///  </summary>
        ///  <remarks>
        ///  The reduction left the tangent of each rotation of columns in place of the element that the rotation cleared.  The rotations are multiplied out
        ///  here, from the last row of the triangle up, into the first n rows and columns of a.
        ///  </remarks>
        private static void X_SVD_P_Prime(int n, double[,] a, int m)
        {
            double[] wrk = new double[2 * m + 1];
            if (n > 1)
            {
                a[n, n] = 1.0;
                a[n - 1, n] = 0.0;
                a[n, n - 1] = 0.0;
                if (n > 2)
                {
                    for (int i1 = n - 2; i1 >= 1; i1--)
                    {
                        a[i1 + 1, i1 + 1] = 1.0;
                        a[i1, i1 + 1] = 0.0;
                        for (int i2 = i1 + 2; i2 <= n; i2++)
                        {
                            X_SVD_Cos_Sin_Tan(-a[i1, i2], out wrk[i2 - 1], out wrk[n + i2 - 2]);
                            a[i1, i2] = 0.0;
                        }
                        int ix1 = i1 + 1;
                        int ix2 = i1;
                        for (int i2 = 1; i2 <= n - i1; i2++)
                        {
                            a[ix1, ix2] = 0.0;
                            ix1 += 1;
                            if (ix1 > m)
                            {
                                ix1 = 1;
                                ix2 += 1;
                            }
                        }
                        for (int i2 = 1; i2 < n - i1; i2++)
                        {
                            if (wrk[i1 + i2] != 1.0 | wrk[n + i1 - 1 + i2] != 0.0)
                            {
                                double temp_cos = wrk[i1 + i2];
                                double temp_sin = wrk[n + i1 - 1 + i2];
                                ix1 = i1;
                                ix2 = i1;
                                for (int i3 = 1; i3 <= n - i1; i3++)
                                {
                                    double temp = a[ix1 + i3, ix2 + i2 + 1];
                                    a[ix1 + i3, ix2 + i2 + 1] = temp_cos * temp - temp_sin * a[ix1 + i3, ix2 + i2];
                                    a[ix1 + i3, ix2 + i2] = temp_sin * temp + temp_cos * a[ix1 + i3, ix2 + i2];
                                }
                            }
                        }
                    }
                }
            }
            a[1, 1] = 1.0;
        }

        /// <summary>
        /// For a rectangular matrix x[1..i, 1..p] where i >= n and a vector b[1..p], return the sum of x[n, j] * b[j] where j in 1..p.
        /// </summary>
        private static double X_SUMPROD(int p, double[] b, double[,] x, int n)
        {
            double sum = 0.0;
            for (int j = 1; j <= p; j++)
                sum += b[j] * x[n, j];
            return sum;
        }

        ///  <summary>
        ///  log Poisson response fitted from linear predictors
        ///  </summary>
        ///  <remarks>IEB July 2009: updated to auto-drop observations at the boundary (complete prediction of outcome)</remarks>
        private static void X_Log_Poisson_Response(int records, double[] linear_predictor, double[] fitted_value, double[] weight, ref int observations, ref bool weighted, ref string dropped)
        {
            // mu = exp(eta).  An eta beyond -log(rounding unit), about 36.7, either way cannot be told from the boundary: the observation is dropped.
            int i;

            double b = -Math.Log(Constant.EPSNEG);
            if (records == observations)
            {
                for (i = 1; i <= records; i++)
                {
                    if (Math.Abs(linear_predictor[i]) > b)
                        BinBound(ref dropped, i, out weighted, ref observations, weight);
                    else
                        fitted_value[i] = Math.Exp(linear_predictor[i]);
                }
            }
            else
            {
                for (i = 1; i <= records; i++)
                {
                    if (weight[i] != 0.0)
                    {
                        if (Math.Abs(linear_predictor[i]) > b)
                            BinBound(ref dropped, i, out weighted, ref observations, weight);
                        else
                            fitted_value[i] = Math.Exp(linear_predictor[i]);
                    }
                    else
                    {
                        fitted_value[i] = 0.0;
                    }
                }
            }
        }

        ///  <summary>
        ///  logistic binomial response fitted from linear predictors
        ///  </summary>
        ///  <remarks>IEB July 2009: updated to auto-drop observations at the boundary (complete prediction of outcome)</remarks>
        private static void X_Logistic_Response(int records, double[] linearPredictor, double[] fittedValue, double[] y_n, double[] weight, ref int observations, ref bool weighted, ref string dropped)
        {
            // mu = t e / (1 + e) with e = exp(eta).  An eta beyond -log(rounding unit), about 36.7, either way gives a p that cannot be told from 0 or 1:
            // the observation is dropped.
            double b = -Math.Log(Constant.EPSNEG);
            if (records == observations)
            {
                for (int i = 1; i <= records; i++)
                {
                    if (Math.Abs(linearPredictor[i]) >= b)
                        BinBound(ref dropped, i, out weighted, ref observations, weight);
                    else
                    {
                        double e = Math.Exp(linearPredictor[i]);
                        fittedValue[i] = y_n[i] * e / (1.0 + e);
                    }
                }
            }
            else
            {
                b = -Math.Log(Constant.EPSNEG);
                for (int i = 1; i <= records; i++)
                {
                    if (weight[i] != 0.0 && y_n[i] != 0.0)
                    {
                        if (Math.Abs(linearPredictor[i]) >= b)
                            BinBound(ref dropped, i, out weighted, ref observations, weight);
                        else
                        {
                            double e = Math.Exp(linearPredictor[i]);
                            fittedValue[i] = y_n[i] * e / (1.0 + e);
                        }
                    }
                    else
                    {
                        fittedValue[i] = 0.0;
                        linearPredictor[i] = 0.0;
                    }
                }
            }
        }

        ///  <remarks>IEB July 2009: updated to auto-drop observations at the boundary (complete prediction of outcome)</remarks>
        private static void BinBound(ref string dropped, int i, out bool weighted, ref int observations, double[] weights)
        {
            // Drops observation i: its weight becomes zero, the count of observations falls by one and it is named in the message.  The fit is treated as
            // weighted from now on, so that the zero weight is respected.
            weighted = true;
            if (weights[i] != 0.0)
            {
                weights[i] = 0.0;
                --observations;
                if (dropped.Length == 0)
                    dropped = "The following observations were dropped due to complete determination of the outcome: " + i.ToString(CultureInfo.CurrentCulture);
                else
                    dropped += ", " + i.ToString(CultureInfo.CurrentCulture);
            }
        }

        /// <summary>
        /// Sets up the Householder reflection that clears a column below its leading element.
        /// </summary>
        /// <remarks>
        /// alpha is the leading element, and the n elements to be cleared start at a[iz1, iz2] and run down the column.  The reflection is H = I - u u',
        /// where u has zeta as its first element and then the n elements as they are left in a on return, scaled so that u'u = 2.  On return alpha holds
        /// what the reflection leaves in the leading position: the length of the whole column, made negative if alpha was positive.  zeta is zero if the
        /// elements to be cleared were already negligible beside alpha, when nothing is changed and no reflection is needed.
        /// LAPACK's DLARFG does the same job, holding the reflection as I - tau v v'.
        /// </remarks>
        private static void X_Setup_Householder_Reflection(int n, ref double alpha, double[,] a, out double zeta, int iz1, int iz2, int m)
        {
            if (n < 1)
            {
                zeta = 0.0;
            }
            else if (n == 1 && a[iz1, iz2] == 0.0)
            {
                zeta = 0.0;
            }
            else
            {
                double beta;
                if (n == 1)
                {
                    if (alpha == 0.0)
                    {
                        zeta = 1.0;
                        alpha = Math.Abs(a[iz1, iz2]);
                        a[iz1, iz2] = -dsign(1, a[iz1, iz2]);
                    }
                    else if (Math.Abs(a[iz1, iz2]) <= Constant.EPSNEG * Math.Abs(alpha))
                    {
                        zeta = 0.0;
                    }
                    else
                    {
                        if (Math.Abs(alpha) >= Math.Abs(a[iz1, iz2]))
                        {
                            beta = Math.Abs(alpha) * Math.Sqrt(1.0 + Math.Pow(a[iz1, iz2] / alpha, 2.0));
                        }
                        else
                        {
                            beta = Math.Abs(a[iz1, iz2]) * Math.Sqrt(1.0 + Math.Pow(alpha / a[iz1, iz2], 2.0));
                        }
                        zeta = Math.Sqrt((Math.Abs(alpha) + beta) / beta);
                        if (alpha >= 0.0)
                        {
                            beta = -beta;
                        }
                        a[iz1, iz2] = -a[iz1, iz2] / (zeta * beta);
                        alpha = beta;
                    }
                }
                else
                {
                    // The length of the elements to be cleared, as scale * sqrt(sumSquares): scale is the largest of them so far, so that squaring can neither
                    // overflow nor underflow.  (sumSquares starts at 1, which does not matter, as scale starts at 0.)
                    double sumSquares = 1.0;
                    double scale = 0.0;
                    int i1 = iz1;
                    int i2 = iz2;
                    for (int ix = 1; ix <= n; ix++)
                    {
                        if (a[i1, i2] != 0.0)
                        {
                            double absxi = Math.Abs(a[i1, i2]);
                            if (scale < absxi)
                            {
                                sumSquares = 1 + sumSquares * Math.Pow(scale / absxi, 2.0);
                                scale = absxi;
                            }
                            else
                            {
                                sumSquares += Math.Pow(absxi / scale, 2.0);
                            }
                        }
                        i1++;
                        if (i1 > m)
                        {
                            i1 = 1;
                            i2++;
                        }
                    }
                    if (scale == 0.0 || scale <= Constant.EPSNEG * Math.Abs(alpha))
                    {
                        zeta = 0.0;
                    }
                    else if (alpha == 0.0)
                    {
                        zeta = 1.0;
                        alpha = scale * Math.Sqrt(sumSquares);
                        i1 = iz1;
                        i2 = iz2;
                        for (int ix = 1; ix <= n; ix++)
                        {
                            a[i1, i2] = -1.0 / alpha * a[i1, i2];
                            i1 += 1;
                            if (i1 > m)
                            {
                                i1 = 1;
                                i2++;
                            }
                        }
                    }
                    else
                    {
                        if (scale < Math.Abs(alpha))
                            beta = Math.Abs(alpha) * Math.Sqrt(1.0 + sumSquares * Math.Pow(scale / alpha, 2.0));
                        else
                            beta = scale * Math.Sqrt(sumSquares + Math.Pow(alpha / scale, 2.0));
                        zeta = Math.Sqrt((beta + Math.Abs(alpha)) / beta);
                        if (alpha > 0.0)
                            beta = -beta;
                        i1 = iz1;
                        i2 = iz2;
                        for (int ix = 1; ix <= n; ix++)
                        {
                            a[i1, i2] = -1.0 / (zeta * beta) * a[i1, i2];
                            i1 += 1;
                            if (i1 > m)
                            {
                                i1 = 1;
                                i2++;
                            }
                        }
                        alpha = beta;
                    }
                }
            }
        }

        /// <summary>
        /// Applies a sequence of plane rotations to an upper triangular matrix from the right, and restores the triangle with rotations from the left.
        /// </summary>
        /// <remarks>
        /// The triangle is the n by n block of a that starts one place beyond row and column ist.  Rotation j, whose cosine and sine are c[j + ist] and
        /// s[j + ist], turns columns j and j + 1, for j from k2 - 1 down to k1.  Each puts an element below the diagonal, at (j + 1, j), and a rotation of
        /// rows j and j + 1 is made at once to take it away; its cosine and sine replace those of the rotation of columns in c and s.  The second loop
        /// applies the rotations of rows to the rest of each row, column by column.
        /// </remarks>
        private static void X_SVD_Rotation_Transform(int n, int k1, int k2, double[] c, double[] s, double[,] a, int m, int ist)
        {
            for (int j = k2 - 1; j >= k1; j--)
            {
                if ((c[j + ist] != 1) | (s[j + ist] != 0))
                {
                    double ctemp = c[j + ist];
                    double stemp = s[j + ist];
                    for (int i = 1; i <= j; i++)
                    {
                        double temp = a[i + ist, j + 1 + ist];
                        a[i + ist, j + 1 + ist] = ctemp * temp - stemp * a[i + ist, j + ist];
                        a[i + ist, j + ist] = stemp * temp + ctemp * a[i + ist, j + ist];
                    }
                    double fill = s[j + ist] * a[j + 1 + ist, j + 1 + ist];
                    a[j + 1 + ist, j + 1 + ist] = c[j + ist] * a[j + 1 + ist, j + 1 + ist];
                    X_SVD_Rotation_Angle(ref a[j + ist, j + ist], ref fill, out c[j + ist], out s[j + ist]);
                }
            }
            for (int j = n; j >= k1 + 1; j--)
            {
                int i1 = Math.Min(k2, j);
                double aij = a[i1 + ist, j + ist];
                for (int i = i1 - 1; i >= k1; i--)
                {
                    double temp = a[i + ist, j + ist];
                    a[i + 1 + ist, j + ist] = c[i + ist] * aij - s[i + ist] * temp;
                    aij = s[i + ist] * aij + c[i + ist] * temp;
                }
                a[k1 + ist, j + ist] = aij;
            }
        }

        ///  <summary>
        ///  Get the angles for the plane rotation
        ///  c = 1/sqrt(1 + t^2) and s = c*t where t = b/a
        ///  </summary>
        ///  <remarks>
        ///  The rotation is the one that turns the pair (a, b) into (r, 0).  On return a holds r, which has the sign of a and the length of the pair, and b
        ///  holds the tangent t, from which the rotation can be worked out again.  If b is zero nothing is turned: c is 1 and s is 0.
        ///  LAPACK's DLARTG does the same job.
        ///  </remarks>
        private static void X_SVD_Rotation_Angle(ref double a, ref double b, out double c, out double s)
        {
            if (b == 0.0)
            {
                c = 1.0;
                s = 0.0;
            }
            else
            {
                double t = X_SVD_Divide_Safely(b, a);
                X_SVD_Cos_Sin_Tan(t, out c, out s);
                a = c * a + s * b;
                b = t;
            }
        }

        /// <summary>
        /// Multiplies elements 1..n in x by alpha.
        /// </summary>
        /// <param name="n">Number of elements in x</param>
        /// <param name="alpha">The scalar by which to multiply each element.</param>
        /// <param name="x">1-based vector.  Postcondition: Elements 1..n are multiplied by alpha.</param>
        /// <remarks>Contains special cases for alpha in {-1.0, 0.0, 1.0} to preserve fidelity.</remarks>
        private static void X_SVD_Vector_by_Scalar(int n, double alpha, double[] x)
        {
            if (n > 0)
            {
                if (alpha == 0.0)
                {
                    for (int ix = 1; ix <= n; ix++)
                        x[ix] = 0.0;
                }
                else if (alpha == -1.0)
                {
                    for (int ix = 1; ix <= n; ix++)
                        x[ix] = -x[ix];
                }
                else if (alpha != 1.0)
                {
                    for (int ix = 1; ix <= n; ix++)
                        x[ix] = alpha * x[ix];
                }
                // else alpha == 1.0, so do nothing.
            }
        }

        /// <summary>
        /// Looks up a bidiagonal matrix, from row n, for an element that is negligible beside its neighbours, where the matrix can be split in two.
        /// </summary>
        /// <remarks>
        /// rowSplit is the row of the first such element met, or 0 if there is none.  If it is the element above the diagonal, rows 1 to rowSplit and the
        /// rows below them are separate problems, and force is false.  If it is the diagonal element, force is true: the rest of its row, or in row n the
        /// column above it, has to be rotated away before the matrix splits.  An element is negligible if it is no more than the rounding unit times the
        /// larger of its neighbours, or if all of them are tiny.
        /// </remarks>
        public static void X_SVD_Test_Bidiagonal_Split(int n, double[] diag, double[] superDiag, out bool force, out int rowSplit)
        {
            const double eps = Constant.EPSNEG;
            const double tiny = Constant.SPREAL / eps;
            force = false;
            int i = n;
            if (n == 1)
            {
                if (Math.Abs(diag[n]) < tiny)
                {
                    force = true;
                    rowSplit = i;
                    return;
                }
            }
            else
            {
                double absDiag = Math.Abs(diag[n]);
                double absSuperDiag = Math.Abs(superDiag[n - 1]);
                double max = Math.Max(absDiag, absSuperDiag);
                if (absDiag <= eps * max || max < tiny)
                {
                    force = true;
                    rowSplit = i;
                    return;
                }
                double maxdi;
                double absdi;
                for (i = n - 1; i >= 2; i--)
                {
                    absdi = Math.Abs(diag[i]);
                    maxdi = Math.Max(absdi, absDiag);
                    max = Math.Max(maxdi, absSuperDiag);
                    if (absSuperDiag <= eps * maxdi || max < tiny)
                    {
                        rowSplit = i;
                        return;
                    }
                    double absei = Math.Abs(superDiag[i - 1]);
                    double emax = Math.Max(absSuperDiag, absei);
                    max = Math.Max(emax, absdi);
                    if (absdi <= eps * emax || max < tiny)
                    {
                        force = true;
                        rowSplit = i;
                        return;
                    }
                    absDiag = absdi;
                    absSuperDiag = absei;
                }
                absdi = Math.Abs(diag[1]);
                maxdi = Math.Max(absdi, absDiag);
                max = Math.Max(maxdi, absSuperDiag);
                if (absSuperDiag <= eps * maxdi || max < tiny)
                {
                    rowSplit = i;
                    return;
                }
                max = Math.Max(absSuperDiag, absdi);
                if (absdi <= eps * absSuperDiag || max < tiny)
                {
                    force = true;
                    rowSplit = i;
                    return;
                }
            }
            i = 0;
            rowSplit = i;
        }

        /// <summary>
        /// Deals with a negligible diagonal element in the last row, n, of a block of a bidiagonal matrix.
        /// </summary>
        /// <remarks>
        /// The element above it, in column n, is rotated into the diagonal element of its own row by a rotation of columns.  That puts an element into
        /// column n one row further up, which the next rotation deals with, and so on up to row 1.  Column n is left with nothing in it above the diagonal.
        /// The cosines and sines are kept in c and s, if doCs is set, to be applied to the rows of z.
        /// </remarks>
        private static void X_SVD_Plane_Rotate(int n, double[] diag, double[] superDiag, bool doCs, double[] c, double[] s)
        {
            if (n > 1)
            {
                int i = n - 1;
                double temp = superDiag[i];
                superDiag[i] = 0;
                X_SVD_Rotation_Angle(ref diag[i], ref temp, out double cs, out double sn);
                if (doCs)
                {
                    c[i] = cs;
                    s[i] = sn;
                }
                for (i = n - 2; i >= 1; i--)
                {
                    temp = -sn * superDiag[i];
                    superDiag[i] = cs * superDiag[i];
                    X_SVD_Rotation_Angle(ref diag[i], ref temp, out cs, out sn);
                    if (doCs)
                    {
                        c[i] = cs;
                        s[i] = sn;
                    }
                }
            }
        }

        /// <summary>
        /// The rotation that starts a QR step on a block of a bidiagonal matrix B.
        /// </summary>
        /// <remarks>
        /// diag and superDiag are the elements of the first row of the block; diagM1, diagN and superDiagM1 are the last two diagonal elements and the
        /// element between them.  The shift is the eigenvalue of the last 2 by 2 block of BB' that is nearer to its last element, diagN squared.  The
        /// rotation is fixed by the first column of B'B less the shift times I, which is (diag^2 - shift, diag * superDiag), here divided through by diag.
        /// superDiagM2 is not used.
        /// </remarks>
        private static void X_SVD_QR_Shift_Parameters(double diag, double superDiag, double diagM1, double diagN, double superDiagM2, double superDiagM1, out double c, out double s)
        {
            double a; double b; double q;

            double top = Math.Pow(diagN * superDiagM1, 2.0);
            if (top == 0.0)
            {
                q = 0.0;
            }
            else
            {
                double f = ((diagM1 - diagN) * (diagM1 + diagN) + Math.Pow(superDiagM1, 2.0)) / 2.0;
                double bottom = f + dsign(1, f) * Math.Sqrt(top + Math.Pow(f, 2.0));
                q = X_SVD_Divide_Safely(top, bottom);
            }
            if (diag != 0.0)
            {
                a = (1.0 - diagN / diag) * (diag + diagN) + q / diag;
                b = superDiag;
            }
            else
            {
                a = 1.0;
                b = 0.0;
            }
            X_SVD_Rotation_Angle(ref a, ref b, out c, out s);
        }

        /// <summary>
        /// One QR step on rows m to n of a bidiagonal matrix.
        /// </summary>
        /// <remarks>
        /// The first rotation, of columns m and m + 1 with cosine c and sine s, puts an element below the diagonal.  Rotations of rows and of columns, in
        /// turn, then chase it down the matrix and off the end, which leaves the matrix bidiagonal again.  The cosines and sines of the rotations of rows
        /// are kept in c_left and s_left, to be applied to the right-hand side, and those of columns in c_right and s_right, to be applied to the rows of
        /// the matrix of right singular vectors, where they are wanted.
        /// </remarks>
        private static void X_SVD_QR_Rotate(int m, int n, double[] diag, double[] super_diag, double c, double s, bool want_left, double[] c_left, double[] s_left, bool want_right, double[] c_right, double[] s_right)
        {
            double cs; double sn;
            int i;

            if (want_right)
            {
                c_right[m] = c;
                s_right[m] = s;
            }
            double temp = c * diag[m] + s * super_diag[m];
            super_diag[m] = c * super_diag[m] - s * diag[m];
            diag[m] = temp;
            temp = s * diag[m + 1];
            diag[m + 1] = c * diag[m + 1];
            for (i = m; i <= n - 2; i++)
            {
                X_SVD_Rotation_Angle(ref diag[i], ref temp, out cs, out sn);
                if (want_left)
                {
                    c_left[i] = cs;
                    s_left[i] = sn;
                }
                temp = cs * super_diag[i] + sn * diag[i + 1];
                diag[i + 1] = cs * diag[i + 1] - sn * super_diag[i];
                super_diag[i] = temp;
                temp = sn * super_diag[i + 1];
                super_diag[i + 1] = cs * super_diag[i + 1];
                X_SVD_Rotation_Angle(ref super_diag[i], ref temp, out cs, out sn);
                if (want_right)
                {
                    c_right[i + 1] = cs;
                    s_right[i + 1] = sn;
                }
                temp = cs * diag[i + 1] + sn * super_diag[i + 1];
                super_diag[i + 1] = cs * super_diag[i + 1] - sn * diag[i + 1];
                diag[i + 1] = temp;
                temp = sn * diag[i + 2];
                diag[i + 2] = cs * diag[i + 2];
            }
            X_SVD_Rotation_Angle(ref diag[n - 1], ref temp, out cs, out sn);
            if (want_left)
            {
                c_left[n - 1] = cs;
                s_left[n - 1] = sn;
            }
            temp = cs * super_diag[n - 1] + sn * diag[n];
            diag[n] = cs * diag[n] - sn * super_diag[n - 1];
            super_diag[n - 1] = temp;
        }

        ///  <summary>
        ///  Return Cos(theta) and Sin(theta) for Tan(theta).
        ///  </summary>
        ///  <remarks>
        ///  cos = 1 / sqrt(1 + t^2) and sin = t cos.  Where t^2 is lost beside 1, or 1 beside t^2, the limits are used: cos is 1 and sin is t for a small t,
        ///  and cos is 1 / |t| and sin is 1 with the sign of t for a large one.
        ///  </remarks>
        private static void X_SVD_Cos_Sin_Tan(double tanTheta, out double cosTheta, out double sinTheta)
        {
            double sqrEps = Math.Sqrt(Constant.EPSNEG);
            double rSqrEps = 1.0 / sqrEps;
            double absTan = Math.Abs(tanTheta);
            if (absTan < sqrEps)
            {
                cosTheta = 1.0;
                sinTheta = tanTheta;
            }
            else if (absTan > rSqrEps)
            {
                cosTheta = 1.0 / absTan;
                sinTheta = dsign(1, tanTheta);
            }
            else
            {
                cosTheta = 1.0 / Math.Sqrt(1.0 + absTan * absTan);
                sinTheta = cosTheta * tanTheta;
            }
        }

        /// <summary>
        /// a / b, guarded against overflow and underflow.
        /// </summary>
        /// <remarks>
        /// Zero if the quotient would be too small to hold.  The reciprocal of the smallest normal number, with the sign of the quotient (of a, if b is
        /// zero), if it would be too large or b is zero.
        /// </remarks>
        private static double X_SVD_Divide_Safely(double a, double b)
        {
            if (a == 0.0)
            {
                return 0.0;
                // err if b is zero
            }

            const double flmin = Constant.SPREAL;
            const double flmax = 1.0 / flmin;
            if (b == 0.0)
            {
                return dsign(flmax, a);
                //err averted
            }

            double absb = Math.Abs(b);
            if (absb >= 1.0)
                return Math.Abs(a) >= absb * flmin ? a / b : 0.0;

            if (Math.Abs(a) <= absb * flmax)
                return a / b;

            // err averted
            double div = flmax;
            if (a < 0.0 && b > 0.0 || a > 0.0 && b < 0.0)
                div = -div;
            return div;
        }

        /// <summary>
        /// The numerical rank: how many of the singular values in x, which are in descending order, come before the first that is negligible.
        /// </summary>
        /// <remarks>A value is negligible if it is no more than the rounding unit times the largest before it.</remarks>
        private static int IsRank(int n, double[] x)
        {
            int k = 0;
            if (n >= 1)
            {
                int ix = 1;
                const double tl = Constant.EPSNEG;
                double xMax = Math.Abs(x[ix]);
                while (k < n)
                {
                    if (Math.Abs(x[ix]) <= tl * xMax)
                        break;
                    if (Math.Abs(x[ix]) > xMax)
                        xMax = Math.Abs(x[ix]);
                    k++;
                    ix++;
                }
            }
            return k;
        }

        private static double Max3(double a, double b, double c)
        {
            double x = a > b ? a : b;
            return c > x ? c : x;
        }

        private static int Min3(int ia, int ib, int ic)
        {
            int ix = ia < ib ? ia : ib;
            if (ic < ix)
                ix = ic;
            return ix;
        }
    }
}
