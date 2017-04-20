using System;
using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    public class LogNaturalAxisScaler: IAxisScaler
    {
        ///  <summary>
        ///  Try to get a neat axis division suitable for values between qmin and qmax.
        ///  </summary>
        ///  <param name="qmin">The smallest value likely to be plotted on the axis. OUTPUT: May be modified if there are no values so that it is 0; will never otherwise be modified.</param>
        ///  <param name="qMinGreaterThanZero">The smallest value greater than zero likely to be plotted on the axis. Used for log scales; may be zero if scaleType is known to be Linear.</param>
        ///  <param name="qmax">The largest value likely to be plotted on the axis. OUTPUT: May be modified if there are no values so that it is 1; will never otherwise be modified.</param>
        public IAxisScale Q_Axis(double qmin, double qMinGreaterThanZero, double qmax, bool isYAxis)
        {
            //  If we have no points at all, the choice is irrelevant so we might as well do it the easy way.
            if (qmin > qmax)
            {
                qmin = 0.0;
                qmax = 1.0;
            }

            //  Start at the first power of 2 smaller than or equal to qmin, stop at the first power of 2 greater than or equal to qmax.
            double scaler = 1.0 / Math.Log(2);
            int minPower = (int)Math.Floor(Math.Log(qMinGreaterThanZero) * scaler);
            int maxPower = qmax <= 0 ? int.MinValue : (int)Math.Ceiling(Math.Log(Math.Max(qmax, qMinGreaterThanZero)) * scaler);
            return new Log2AxisScale(qmin, qmax, minPower, maxPower);
        }
    }
}
