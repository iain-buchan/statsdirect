using System;
using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    internal class Log10AxisMasker : IAxisMasker
    {
        string IAxisMasker.AxisMask(IAxisScale axisScale)
        {
            return "G";
        }
    }
}