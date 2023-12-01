using System;

namespace StatsDirect.Templates
{
    /// <summary>
    /// Immutable.
    /// </summary>
    [Serializable]
    public class ScaleParameters
    {
        public AxisScaleParameters X { get; }
        public AxisScaleParameters Y { get; }

        public ScaleParameters(AxisScaleParameters x, AxisScaleParameters y)
        {
            X = x;
            Y = y;
        }
    }
}
