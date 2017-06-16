using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    public interface IAxisMasker
    {
        /// <summary>
        /// Work out a sensible axis mask for the given linear scale
        /// </summary>
        string AxisMask(IAxisScale axisScale);
    }
}
