using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StatsDirect.Charting
{
    /// <summary>
    /// A tic is a mark on a chart axis.  It appears at some point on the axis (stored in chart co-ordinates) and is a major (large) or minor (small) tic.
    /// </summary>
    public class Tic
    {
        public double Value { get; set; }
        public TicType TicType { get; set; }
    }

    public enum TicType
    {
        Major,
        Minor
    }
}
