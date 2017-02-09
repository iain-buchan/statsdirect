using System;
using System.Collections.Generic;

namespace Layout.Formatters
{
    interface IFormatter
    {
        Axis Format(List<Axis> list, List<Format> formats, AxisLabeler.Options options, Func<Axis, double> ScoreAxis, double bestScore = double.NegativeInfinity);
    }

}
