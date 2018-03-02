using System;
using StatsDirect.Templates;

namespace StatsDirect.Charting.Scales
{
    public class Log10AxisScaler: IAxisScaler
    {
        ///  <summary>
        ///  Try to get a neat axis division suitable for values between minimumDataValue and maximumDataValue.
        ///  </summary>
        ///  <param name="minimumDataValue">The smallest value likely to be plotted on the axis.</param>
        ///  <param name="minimumDataValueGreaterThanZero">The smallest value greater than zero likely to be plotted on the axis. Used for log scales; may be zero if scaleType is known to be Linear.</param>
        ///  <param name="maximumDataValue">The largest value likely to be plotted on the axis.</param>
        /// <param name="isYAxis"></param>
        public IAxisScale QAxis(double _, double minimumDataValueGreaterThanZero, double maximumDataValue, bool isYAxis, bool useDataValuesAsScaleValues)
        {
            if (minimumDataValueGreaterThanZero <= 0)
                throw new Exception("Cannot create a log axis with a minimum value less than or equal to zero");

            //  Start at the first power of 10 smaller than or equal to minimumDataValueGreaterThanZero, stop at the first power of 10 greater than or equal to maximumDataValue.
            int minPower = (int)Math.Floor(Math.Log10(minimumDataValueGreaterThanZero));
            int maxPower = maximumDataValue <= 0 ? int.MinValue : (int)Math.Ceiling(Math.Log10(Math.Max(minimumDataValueGreaterThanZero, maximumDataValue)));
            int minimumScaleTicMultiplier = 1;
            int maximumScaleTicMultiplier = 10;
            int candidateDivisions = maxPower - minPower;
            int[] minorTicMultipliers;
            if (candidateDivisions <= 5)
            {
                minorTicMultipliers = new[] { 2, 5 };
                foreach (int candidateScaleTicMultiplier in minorTicMultipliers)
                    if (Math.Pow(10, minPower) * candidateScaleTicMultiplier <= minimumDataValueGreaterThanZero)
                        minimumScaleTicMultiplier = candidateScaleTicMultiplier;
                foreach (int candidateScaleTicMultiplier in minorTicMultipliers)
                    if (Math.Pow(10, maxPower - 1) * candidateScaleTicMultiplier >= maximumDataValue)
                    {
                        maximumScaleTicMultiplier = candidateScaleTicMultiplier;
                        break;
                    }
            }
            else
                minorTicMultipliers = new int[0];
            return new Log10AxisScale(minimumDataValueGreaterThanZero, maximumDataValue, minPower, minimumScaleTicMultiplier, maxPower, maximumScaleTicMultiplier, minorTicMultipliers);
        }
    }
}
