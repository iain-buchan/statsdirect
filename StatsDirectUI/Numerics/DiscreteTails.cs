using System;

namespace StatsDirect.Numerics
{
    /// <summary>
    /// The tails of a count whose probabilities rise to those of one most probable value and fall away from it, as those of a
    /// binomial and of a hypergeometric count do, from the ratio of the probabilities of two values that are next to each other.
    /// </summary>
    /// <remarks>
    /// Each probability is worked out in proportion to that of the most probable value, from the one next to it.  A sum of them
    /// is made from a value outwards, until a term is below 1 part in 10^20 of the sum: the sum of them all, from the most
    /// probable value both ways, makes them probabilities; a tail is the sum from the value observed outwards; and the sum of the
    /// probabilities that are no more than that of the value observed is the sum, each way, from the first value whose
    /// probability is no more than it.  No probability is taken from 1 to give a small one, and the time taken is as the
    /// standard deviation of the count and not as the number of values that it can have.
    /// </remarks>
    public static class DiscreteTails
    {
        /// <summary>
        /// The probabilities of a count that is no more than the value observed, no less than it, and the value itself; and
        /// the sum of the probabilities of the values that are no more probable than the value observed.
        /// </summary>
        /// <param name="first">The least value that the count can have.</param>
        /// <param name="last">The greatest value that the count can have.</param>
        /// <param name="mode">The most probable value, or a value near to it.</param>
        /// <param name="observed">The value observed.</param>
        /// <param name="up">For a value k below the greatest, the probability of k + 1 over that of k.</param>
        /// <param name="down">For a value k above the least, the probability of k - 1 over that of k.</param>
        /// <param name="lower">On return, the probability of a count that is no more than the value observed.</param>
        /// <param name="upper">On return, the probability of a count that is no less than the value observed.</param>
        /// <param name="point">On return, the probability of the value observed.</param>
        /// <param name="noMoreProbable">On return, the sum of the probabilities of the values whose probability is no more
        /// than that of the value observed, or above it by no more than 1 part in 10^7; no more than 1.</param>
        public static void Sum(long first, long last, long mode, long observed, Func<long, double> up, Func<long, double> down, out double lower, out double upper, out double point, out double noMoreProbable)
        {
            if (observed < first || observed > last)
            {
                // a value that the count cannot have
                lower = observed < first ? 0.0 : 1.0;
                upper = observed < first ? 1.0 : 0.0;
                point = 0.0;
                noMoreProbable = 0.0;
                return;
            }
            mode = Math.Max(first, Math.Min(last, mode));

            // A probability in proportion to that of the most probable value is held as a number and the count of the factors of
            // 2^-512 that have been taken out of it, so that it stays within the range of the numbers.  With three of them it is
            // below the least number above nothing
            const double factor = 1.3407807929942597E+154;   // 2^512
            const int nothing = 3;
            void Next(ref double term, ref int count, double ratio)
            {
                term *= ratio;
                if (term < 1.0 / factor)
                {
                    term *= factor;
                    count++;
                }
            }
            // whether x is no more than y, or above it by no more than 1 part in 10^7: two values can have the same probability,
            // and they are not to be parted by the rounding of their last figures
            bool NoMore(double x, int xCount, double y, int yCount)
            {
                if (xCount == yCount)
                    return x <= y * (1.0 + 1.0E-7);
                if (xCount == yCount + 1)
                    return x / factor <= y * (1.0 + 1.0E-7);
                if (xCount == yCount - 1)
                    return x <= y / factor * (1.0 + 1.0E-7);
                return xCount > yCount;
            }
            // the sum of the probabilities from the value k outwards (step -1: downwards; 1: upwards), over that of k; what each
            // addition loses to rounding is carried to the next
            double Outwards(long k, int step)
            {
                double sum = 1.0;
                double lost = 0.0;
                double term = 1.0;
                while (step < 0 ? k > first : k < last)
                {
                    term *= step < 0 ? down(k) : up(k);
                    k += step;
                    if (term < 1.0E-20 * sum)
                        break;
                    double y = term - lost;
                    double s = sum + y;
                    lost = s - sum - y;
                    sum = s;
                }
                return sum;
            }

            // the sum of them all, over the probability of the most probable value, which is in both parts
            double total = Outwards(mode, -1) + Outwards(mode, 1) - 1.0;
            double Probability(double x, int count) => count >= nothing ? 0.0 : Math.ScaleB(x / total, -512 * count);

            // the value observed
            double term0 = 1.0;
            int count0 = 0;
            for (long k = mode; k > observed && count0 < nothing; k--)
                Next(ref term0, ref count0, down(k));
            for (long k = mode; k < observed && count0 < nothing; k++)
                Next(ref term0, ref count0, up(k));
            point = Probability(term0, count0);

            // the tail that the value observed is in is added up from it outwards; the other is what is left, with the value observed
            int away = observed > mode ? 1 : -1;
            double beyond = Outwards(observed, away);
            double tail = Probability(term0 * beyond, count0);
            double rest = 1.0 - Probability(term0 * (beyond - 1.0), count0);
            lower = away < 0 ? tail : rest;
            upper = away < 0 ? rest : tail;

            // each way from the most probable value, the first value that is no more probable than the value observed, and the
            // values beyond it
            noMoreProbable = 0.0;
            {
                double term = 1.0;
                int count = 0;
                long k = mode;
                while (!NoMore(term, count, term0, count0) && k > first && count < nothing)
                {
                    Next(ref term, ref count, down(k));
                    k--;
                }
                if (NoMore(term, count, term0, count0))
                    noMoreProbable += Probability(term * Outwards(k, -1), count);
            }
            if (mode < last)
            {
                double term = 1.0;
                int count = 0;
                Next(ref term, ref count, up(mode));
                long k = mode + 1;
                while (!NoMore(term, count, term0, count0) && k < last && count < nothing)
                {
                    Next(ref term, ref count, up(k));
                    k++;
                }
                if (NoMore(term, count, term0, count0))
                    noMoreProbable += Probability(term * Outwards(k, 1), count);
            }
            noMoreProbable = Math.Min(noMoreProbable, 1.0);
        }
    }
}
