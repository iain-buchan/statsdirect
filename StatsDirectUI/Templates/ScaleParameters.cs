using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StatsDirect.Templates
{
    [Serializable]
    public class ScaleParameters
    {
        public AxisScaleParameters X { get; set; }
        public AxisScaleParameters Y { get; set; }

        public ScaleParameters()
        {
            X = new AxisScaleParameters();
            Y = new AxisScaleParameters();
        }

        public ScaleParameters Clone()
        {
            return new ScaleParameters { X = X.Clone(), Y = Y.Clone() };
        }
    }
}
