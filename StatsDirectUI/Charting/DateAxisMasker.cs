using System.Globalization;
using StatsDirect.Templates;
using System;

namespace StatsDirect.Charting
{
    public class DateAxisMasker : IAxisMasker
    {
        public string AxisMask(IAxisScale axisScale)
        {
            DateTime minimumScaleDate = DateTime.FromOADate(axisScale.MinimumScaleValue);
            DateTime maximumScaleDate = DateTime.FromOADate(axisScale.MaximumScaleValue);
            TimeSpan scaleInterval = maximumScaleDate.Subtract(minimumScaleDate);
            if (scaleInterval.TotalHours < 15)
            {
                // Use hours
                return CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern;
            }
            else if (scaleInterval.TotalDays < 3)
            {
                // Use days + hours
                return CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern + " " + CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern;
            }
            else
            {
                // Days or longer
                return CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
            }
        }
    }
}
