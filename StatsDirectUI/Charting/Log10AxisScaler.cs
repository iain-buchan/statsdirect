using System;
using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    public class Log10AxisScaler: IAxisScaler
    {
        ///  <summary>
        ///  Try to get a neat axis division suitable for values between qmin and qmax.
        ///  </summary>
        ///  <param name="qmin">The smallest value likely to be plotted on the axis.</param>
        ///  <param name="qMinGreaterThanZero">The smallest value greater than zero likely to be plotted on the axis. Used for log scales; may be zero if scaleType is known to be Linear.</param>
        ///  <param name="qmax">The largest value likely to be plotted on the axis.</param>
        public IAxisScale Q_Axis(double qmin, double qMinGreaterThanZero, double qmax, bool isYAxis)
        {
            //  If we have no points at all, the choice is irrelevant so we might as well do it the easy way.
            if (qmin > qmax)
            {
                qmin = 0.0;
                qmax = 1.0;
            }

            //  Start at the first power of 10 smaller than or equal to qminGreaterThanZero, stop at the first power of 10 greater than or equal to qmax.
            int minPower = (int)Math.Floor(Math.Log10(qMinGreaterThanZero));
            int maxPower = qmax <= 0 ? int.MinValue : (int)Math.Ceiling(Math.Log10(Math.Max(qMinGreaterThanZero, qmax)));
            int minimumScaleTicMultiplier = 1;
            int maximumScaleTicMultiplier = 10;
            int candidateDivisions = maxPower - minPower;
            int[] minorTicMultipliers;
            if (candidateDivisions <= 5)
            {
                minorTicMultipliers = new int[] { 2, 5 };
                foreach (int candidateScaleTicMultiplier in minorTicMultipliers)
                    if (Math.Pow(10, minPower) * candidateScaleTicMultiplier <= qmin)
                        minimumScaleTicMultiplier = candidateScaleTicMultiplier;
                foreach (int candidateScaleTicMultiplier in minorTicMultipliers)
                    if (Math.Pow(10, maxPower - 1) * candidateScaleTicMultiplier >= qmax)
                    {
                        maximumScaleTicMultiplier = candidateScaleTicMultiplier;
                        break;
                    }
            }
            else
                minorTicMultipliers = new int[0];
            return new Log10AxisScale(qmin, qmax, minPower, minimumScaleTicMultiplier, maxPower, maximumScaleTicMultiplier, minorTicMultipliers);
        }
    }
}
