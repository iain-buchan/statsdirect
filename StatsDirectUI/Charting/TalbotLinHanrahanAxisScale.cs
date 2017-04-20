using System;
using System.Collections.Generic;
using System.Linq;
using Layout;
using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    class TalbotLinHanrahanAxisScale : ILinearAxisScale
    {
        private Axis TlhAxis { get; set; }
        private double QMin { get; set; }
        private double QMax { get; set; }

        public TalbotLinHanrahanAxisScale(Axis tlhAxis, double qmin, double qmax)
        {
            TlhAxis = tlhAxis;
            QMin = qmin;
            QMax = qmax;
        }

        public double MaximumDataValue => QMax;

        public double MaximumScaleValue => TlhAxis.VisibleRange.Max;

        public double MinimumDataValue => QMin;

        public double MinimumScaleValue => TlhAxis.VisibleRange.Min;

        public int IntervalsPerMajorTic => 1;

        public int Phase => 0;

        public double Interval => (double)(TlhAxis.Labels[1].Item1 - TlhAxis.Labels[0].Item1);

        public double FirstMajorTicValue => (double)TlhAxis.Labels[0].Item1;

        public IList<Tic> Tics()
        {
            return TlhAxis.Labels.Select(label => new Tic { TicType = TicType.Major, Value = Convert.ToDouble(label.Item1) }).ToList();
        }
    }
}
