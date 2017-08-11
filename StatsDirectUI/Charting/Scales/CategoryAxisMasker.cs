using System;
using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    public class CategoryAxisMasker : IAxisMasker
    {
        public string AxisMask(IAxisScale axisScale)
        {
            throw new NotImplementedException("Would never expect to be asked for an axis mask for a category axis");
        }
    }
}
