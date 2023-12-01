using StatsDirect.Charting;
using System;
using System.Collections.Generic;

namespace StatsDirect.Templates
{
    [Serializable]
    public class AxisScaleParameters
    {
        public IReadOnlySet<ScaleType> AllowedScaleTypes { get; }
        public IAxisScale? AxisScale { get; init; }
        public DashStyleDescriptor GridLineDashStyle { get; init; }
        public bool HasGridLines { get; init; }
        public LabelDirection LabelDirection { get; init; }
        public double? MarkerLineValue { get; init; }
        public double Max { get; init; }
        public double Min { get; init; }
        public double MinGreaterThanZero { get; init; }
        public ScaleType ScaleType { get; init; }

        public AxisScaleParameters(IReadOnlySet<ScaleType> allowedScaleTypes)
        {
            AllowedScaleTypes = allowedScaleTypes;
        }

        public AxisScaleParameters(IReadOnlyCollection<ScaleType> allowedScaleTypes)
            : this(new HashSet<ScaleType>(allowedScaleTypes))
        {
        }

        public AxisScaleParameters()
            : this(new HashSet<ScaleType>())
        {
        }

        public AxisScaleParameters(AxisScaleParameters template)
        {
            AllowedScaleTypes = template.AllowedScaleTypes;
            AxisScale = template.AxisScale;
            GridLineDashStyle = template.GridLineDashStyle;
            HasGridLines = template.HasGridLines;
            LabelDirection = template.LabelDirection;
            MarkerLineValue = template.MarkerLineValue;
            Max = template.Max;
            Min = template.Min;
            MinGreaterThanZero = template.MinGreaterThanZero;
            ScaleType = template.ScaleType;
        }
    }
}
