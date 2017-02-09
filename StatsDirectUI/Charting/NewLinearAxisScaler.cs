using StatsDirect.Templates;
using System;

namespace StatsDirect.Charting
{
    class NewLinearAxisScaler: IAxisScaler
    {
        // From http://stackoverflow.com/questions/8506881/nice-label-algorithm-for-charts-with-minimum-ticks/16363437#16363437
        // In turn from "Graphics Gems, Volume 1" by Andrew S. Glassner
        private double maxTicks = 30;

        /// <summary>
        /// Calculate and update values for tick spacing and nice minimum and maximum data points on the axis.
        /// </summary>
        /// <param name="qmin"></param>
        /// <param name="qmax"></param>
        /// <param name="something"></param>
        /// <returns></returns>

        public IAxisScale Q_Axis(double qmin, double qMinGreaterThanZero, double qmax, bool isYAxis)
        {
            double range = niceNum(qmax - qmin, false);
            double tickSpacing = niceNum(range / (maxTicks - 1), true);
            double niceMin = Math.Floor(qmin / tickSpacing) * tickSpacing;
            double niceMax = Math.Ceiling(qmax / tickSpacing) * tickSpacing;
            // Look up to intervalsPerMajorTic along to find the tic with the lowest number of significant digits; use that as the phase.
            int intervalsPerMajorTic = 5;
            int bestPhase = 0;
            int bestSignificantDigits = int.MaxValue;
            int bestExponent = int.MinValue;
            // Look for the best place to start labelling.  Prefer labels that have the fewest significant digits (so 0.1 rather than 0.15).  Within that, prefer labels that are larger (so prefer 10 to 5 or 1, but also prefer 10 to 11).
            for (int i = 0; niceMin + tickSpacing * i <= niceMax; i++)
            {
                double value = niceMin + i * tickSpacing;
                int exponent = 0 == value ? 0 : (int)Math.Floor(Math.Log10(Math.Abs(value)));
                int sd = SignificantDigits(value);
                if (sd < bestSignificantDigits || sd == bestSignificantDigits && exponent > bestExponent)
                {
                    bestPhase = i;
                    bestSignificantDigits = sd;
                    bestExponent = exponent;
                }
            }
            return new NewLinearAxisScale(qmin, qmax, niceMin, niceMax, tickSpacing, intervalsPerMajorTic, bestPhase);
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

        /**
         * Returns a "nice" number approximately equal to range.
         * Rounds the number if round = true
         * Takes the ceiling if round = false.
         *
         * @param range the data range
         * @param round whether to round the result
         * @return a "nice" number to be used for the data range
         */
        private static double niceNum(double range, bool round)
        {
            double exponent = Math.Floor(Math.Log10(Math.Abs(range)));
            double mantissa = range / Math.Pow(10, exponent);

            double niceMantissa;
            if (round)
            {
                if (mantissa < 1.5)
                    niceMantissa = 1;
                else if (mantissa < 3)
                    niceMantissa = 2;
                else if (mantissa < 7)
                    niceMantissa = 5;
                else
                    niceMantissa = 10;
            }
            else
            {
                if (mantissa <= 1)
                    niceMantissa = 1;
                else if (mantissa <= 2)
                    niceMantissa = 2;
                else if (mantissa <= 5)
                    niceMantissa = 5;
                else
                    niceMantissa = 10;
            }

            return niceMantissa * Math.Pow(10, exponent);
        }
    }
}
