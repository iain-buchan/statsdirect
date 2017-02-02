using System;

using StatsDirect.Templates;
using StatsDirect.Numerics;

namespace StatsDirect.Charting
{
    public class LinearAxisScaler: IAxisScaler
    {
        ///  <summary>
        ///  Try to get a neat axis division suitable for values between qmin and qmax.
        ///  </summary>
        ///  <param name="qmin">The smallest value likely to be plotted on the axis.</param>
        ///  <param name="qMinGreaterThanZero">The smallest value greater than zero likely to be plotted on the axis. Used for log scales; not used for LinearAxisScaler.</param>
        ///  <param name="qmax">The largest value likely to be plotted on the axis.</param>
        ///  <remarks></remarks>
        public IAxisScale Q_Axis(double qmin, double qMinGreaterThanZero, double qmax)
        {
            //  If we have no points at all, the choice is irrelevant so we might as well do it the easy way.
            if (qmin > qmax)
            {
                qmin = 0.0;
                qmax = 1.0;
            }

            // Short-circuit for the common case of a 0 to 1 axis.
            if (qmin == 0.0 && qmax == 1.0)
                return new LinearAxisScale(qmin, qmax, qmin, qmax, 20, 5);

            int[] divisionsToTry = new int[] { 20, 15, 25, 16, 24 };
            int bestScoreSoFar = int.MaxValue; // Lower is better
            LinearAxisScale bestScaleSoFar = null;
            foreach (int candidateDivisions in divisionsToTry)
            {
                LinearAxisScale unshiftedAxisScale = Axis(qmin, qmax, qmin, qmax, candidateDivisions);
                int shiftedScore;
                LinearAxisScale shiftedAxisScale = ShiftMin(qmin, qmax, unshiftedAxisScale, out shiftedScore);
                NeatnessComparison neater = CompareNeatness(bestScaleSoFar, shiftedAxisScale);
                if (neater == NeatnessComparison.Second || (neater == NeatnessComparison.Equal && shiftedScore < bestScoreSoFar))
                {
                    bestScoreSoFar = shiftedScore;
                    bestScaleSoFar = shiftedAxisScale;
                }
            }
            return bestScaleSoFar;
        }

        private enum NeatnessComparison
        {
            First,
            Equal,
            Second
        }

        private static NeatnessComparison CompareNeatness(LinearAxisScale first, LinearAxisScale second)
        {
            // If one of the scales doesn't exist (first won't on the first iteration), the other is automatically better
            if (null == first)
                return NeatnessComparison.Second;
            if (null == second)
                return NeatnessComparison.First;

            // Score the neatness of the two scales - lower is better.
            int firstScore = SignificantDigits(first.MinimumScaleValue) + SignificantDigits(first.FirstMajorTicValue);
            int secondScore = SignificantDigits(second.MinimumScaleValue) + SignificantDigits(second.FirstMajorTicValue);

            // If the axis doesn't span zero, that's it.
            if (!(first.MinimumScaleValue < 0.0 && first.MaximumScaleValue > 0.0))
                return secondScore == firstScore ? NeatnessComparison.Equal : secondScore < firstScore ? NeatnessComparison.Second : NeatnessComparison.First;

            // The axis spans zero. In this case, prefer the scale that hits zero on the way past, if there is one.  If neither do, use the earlier preference.
            const double tolerance = Constant.EPSNEG * 10.0;
            bool firstAxisScaleHitsZero = false;
            foreach (Tic tic in first.Tics())
                if (Math.Abs(tic.Value) < tolerance)
                {
                    firstAxisScaleHitsZero = true;
                    break;
                }

            if (secondScore < firstScore || !firstAxisScaleHitsZero)
            {
                foreach (Tic tic in second.Tics())
                    if (Math.Abs(tic.Value) < tolerance)
                        return NeatnessComparison.Second;

                // If we get here, the second scale doesn't hit zero. Either the second score is better or the first scale doesn't hit it either, however!
                return secondScore == firstScore ? NeatnessComparison.Equal : secondScore < firstScore ? NeatnessComparison.Second : NeatnessComparison.First;
            }

            // If we get here, secondScore is worse than firstScore and firstAxisScaleHitsZero, so the first scale is unambiguously better.
            return NeatnessComparison.First;
        }

        /// <summary>
        /// Given x, return the number of significant digits less 1 (the number of decimal places if the number was represented as n.nnnnnEnnn).
        /// </summary>
        /// <param name="x"></param>
        private static int SignificantDigits(double x)
        {
            if (x == 0.0)
                return 0;

            int ipow = ((int)(Math.Floor(Math.Log10(Math.Abs(x))))) + 1;
            double sc = x / (Math.Pow(10.0, ipow));
            if (sc == 1.0)
                return 1;
            else
                return sc.ToString().Length - 2;
        }

        ///  <summary>
        ///  
        ///  </summary>
        ///  <param name="qmin">Smallest data value</param>
        ///  <param name="qmax">Largest data value</param>
        ///  <param name="zmin">Axis minimum (output)</param>
        ///  <param name="zint">Axis intervals</param>
        ///  <param name="div"></param>
        ///  <param name="score">A score on an arbitrary scale of the "look" of this axis; lower scores are better</param>
        ///  <remarks></remarks>
        private static LinearAxisScale ShiftMin(double qmin, double qmax, LinearAxisScale unshiftedAxisScale, out int score)
        {
            // data must cover target% of interval
            const double TARGET_COVERAGE = 0.7;

            // shift zmin to a nice spot
            double zmin = unshiftedAxisScale.MinimumScaleValue;
            int div = unshiftedAxisScale.Intervals;
            double zint = unshiftedAxisScale.Interval;
            if (zmin != 0.0)
            {
                // ipow is the first power of 10 above zmin - if zmin is 10.1, for example, zmin will be 2 as 100 (10^2) is the first power of 10 above 10.1.
                int ipow = ((int)(Math.Floor(Math.Log10(Math.Abs(zmin))))) + 1;
                double[] ztry = new double[] { 1.0, 0.5, 0.1, 0.2, 0.3, 0.4, 0.6, 0.8, 0.7, 0.9, 0.15, 0.25, 0.75, 0.05 };
                foreach (double candidate in ztry)
                {
                    double nzmin = candidate * Math.Pow(10.0, ipow);
                    if (zmin < 0.0)
                        nzmin = -nzmin;
                    double nzmax = nzmin + zint * div;
                    if (nzmin <= qmin & nzmax >= qmax)
                    {
                        double coverage = Math.Abs(qmax - qmin) / Math.Abs(nzmax - nzmin);
                        if (coverage > TARGET_COVERAGE)
                        {
                            zmin = nzmin;
                            break;
                        }
                    }
                    else
                    {
                        if (nzmax < qmax)
                        {
                            if (div % 5 == 0)
                            {
                                if (div < 25)
                                {
                                    nzmax = nzmin + zint * (div + 5);
                                    if (nzmin <= qmin & nzmax >= qmax)
                                    {
                                        double coverage = Math.Abs(qmax - qmin) / Math.Abs(nzmax - nzmin);
                                        if (coverage > TARGET_COVERAGE)
                                        {
                                            zmin = nzmin;
                                            div += 5;
                                            break;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (div < 24)
                                {
                                    nzmax = nzmin + zint * (div + 4);
                                    if (nzmin <= qmin && nzmax >= qmax)
                                    {
                                        double coverage = Math.Abs(qmax - qmin) / Math.Abs(nzmax - nzmin);
                                        if (coverage > TARGET_COVERAGE)
                                        {
                                            zmin = nzmin;
                                            div += 4;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // score the look of the ticks and labelled intervals
            score = ScoreLook(zint);
            int intervalsPerMajorTic = div % 5 == 0 ? 5 : 4;
            score += ScoreLook(zint * intervalsPerMajorTic);

            return new LinearAxisScale(qmin, qmax, zmin, zmin + div * zint, div, intervalsPerMajorTic);
        }

        /// <summary>
        /// Lower scores are better.
        /// </summary>
        /// <param name="z"></param>
        /// <returns></returns>
        private static int ScoreLook(double z)
        {
            int ipow = ((int)(Math.Floor(Math.Log10(Math.Abs(z))))) + 1;
            double scaled = z / (Math.Pow(10.0, ipow));
            if (scaled == 0.0 || scaled == 1.0)
                return 0;
            if (scaled == 0.5 || scaled == 0.1)
                return 1;
            if (scaled == 0.15 || scaled == 0.2 || scaled == 0.25 || scaled == 0.3)
                return 2;
            if (scaled == 0.4 || scaled == 0.6)
                return 3;
            return 4;
        }

        private static LinearAxisScale Axis(double qmin, double qmax, double zmn, double zmx, int nstep)
        {
            // Check for crazy values
            if (zmn == Constant.MISSING || zmx == Constant.MISSING || double.IsInfinity(zmn) || double.IsInfinity(zmx) || double.IsNaN(zmn) || double.IsNaN(zmx) || nstep < 1)
                return new LinearAxisScale(0, 0, 0, 0, 1, 1);

            if (Math.Abs(zmn - zmx) < 1e-10)
            {
                zmn -= 1.0;
                zmx += 1.0;
            }

            double rint = (zmx - zmn) / (nstep + 0.1);
            if (rint <= 0.0)
                return new LinearAxisScale(0, 0, 0, 0, 1, 1);

            // Nothing too crazy going on.  What can we make?
            double[] r = { 0.1, 0.15, 0.2, 0.25, 0.4, 0.5, 0.6, 0.75, 0.8 };
            double znmin, znmax, zstep;
            do
            {
                int mint = Convert.ToInt32(Math.Log10(rint) - 2.0);
                double tenn = Math.Pow(10.0, mint);
                bool jump = false;
                double ar = 0;
                for (int j = 1; j <= 11; j++)
                {
                    foreach (double candidate in r)
                    {
                        if (candidate * tenn >= rint)
                        {
                            ar = candidate;
                            jump = true;
                            break;
                        }
                    }
                    if (jump)
                        break;
                    tenn *= 10.0;
                }
                zstep = tenn * ar;

                ar = zstep * Math.Floor((1.0 + 2.0 * Constant.EPSNEG) * zmn / zstep);
                if (double.IsInfinity(ar))
                    return new LinearAxisScale(0, 0, 0, 0, 1, 1);

                while (!((ar - zstep * 0.05) <= zmn))
                    ar -= zstep;
                znmin = ar;
                znmax = znmin + zstep * (nstep + 0.04);
                if (znmax >= zmx)
                    break;
                rint *= 1.05;
            }
            while (true);
            int maxA = Convert.ToInt32(Math.Log10(Math.Abs(znmax)) + 1.0);
            if (Math.Abs(znmin) > Math.Abs(znmax))
                maxA = Convert.ToInt32(Math.Log10(Math.Abs(znmin)) + 1.0);
            if (maxA < 0)
                maxA = 0;
            int maxB = Convert.ToInt32(Math.Log10(zstep) - 2.5);
            if (maxB > 0)
                maxB = 0;
            maxB = -maxB;
            if (maxA + maxB >= 10)
                return new LinearAxisScale(qmin, qmax, znmin, znmin + zstep * nstep, nstep, 1);
            double znm = znmin;
            for (int i = 0; i <= 9; i++)
            {
                if (znm + zstep * (nstep + 0.04) < zmx)
                    break;
                znmin = znm;
                znm = znmin * Math.Pow(10.0, (maxB - i));
                if (znm < 0.0)
                    znm = znm - 1.0;
                znm = Math.Floor(znm) / Math.Pow(10.0, (maxB - i));
            }
            return new LinearAxisScale(qmin, qmax, znmin, znmin + zstep * nstep, nstep, 1);
        }

        public static LinearAxisScale v_axis(double qmin, double qmax, int cm)
        {
            if (cm > 0)
                return Axis(qmin, qmax, qmin, qmax, cm);
            else
                return new LinearAxisScale(qmin, qmax, qmin, qmax, 1, 1);
        }
    }
}
