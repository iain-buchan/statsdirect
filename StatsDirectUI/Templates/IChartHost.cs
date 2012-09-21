using System;
using System.Collections.Generic;

namespace StatsDirect.Templates
{
    public enum ScaleType
    {
        NotSet = -1,
        Linear = 0,
        LogNatural = 1,
        Log10 = 2,
        Date = 3,
        Category = 4
    }

    public enum LabelDirection
    {
        Across = 0,
        Up = 1,
        Down = 2,
        SlopeUp = 3,
        SlopeDown = 4
    }

    [Serializable]
    public class AxisScaleParameters
    {
        public bool ShouldCheck { get; set; }
        public double Min { get; set; }
        public double Max { get; set; }
        public ICollection<ScaleType> AllowedScaleTypes { get; set; }
        public ScaleType ScaleType { get; set; }

        // Scale
        public bool HasAxisScale { get; set; }
        public double QMin { get; set; }
        public double QMax { get; set; }
        public int Div { get; set; }
        public double ZMin { get; set; }
        public double ZInt { get; set; }
        public int MinorTicsPerMajorTic { get; set; }
        public string Mask { get; set; }
        public LabelDirection LabelDirection { get; set; }

        // Grid lines
        public bool HasGridLines { get; set; }
        public System.Drawing.Drawing2D.DashStyle GridLineDashStyle { get; set; }

        // Marker line
        public bool HasMarkerLine { get; set; }
        public double MarkerLineValue { get; set; }

        public AxisScaleParameters Clone()
        {
            return (AxisScaleParameters)MemberwiseClone();
        }
    }

    [Serializable]
    public class ScaleParameters
    {
        public AxisScaleParameters X;
        public AxisScaleParameters Y;

        public ScaleParameters()
        {
            X = new AxisScaleParameters();
            Y = new AxisScaleParameters();
        }

        public ScaleParameters Clone()
        {
            return new ScaleParameters {X = X.Clone(), Y = Y.Clone()};
        }
    }
}
