using StatsDirect.Templates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StatsDirect.Charting
{
    class TalbotLinHanrahanAxisScale : ILinearAxisScale
    {
        private Layout.Axis TlhAxis { get; set; }
        private double QMin { get; set; }
        private double QMax { get; set; }

        public TalbotLinHanrahanAxisScale(Layout.Axis tlhAxis, double qmin, double qmax)
        {
            TlhAxis = tlhAxis;
            QMin = qmin;
            QMax = qmax;
        }

        public double MaximumDataValue
        {
            get
            {
                return QMax;
            }
        }

        public double MaximumScaleValue
        {
            get
            {
                return TlhAxis.VisibleRange.Max;
            }
        }

        public double MinimumDataValue
        {
            get
            {
                return QMin;
            }
        }

        public double MinimumScaleValue
        {
            get
            {
                return TlhAxis.VisibleRange.Min;
            }
        }

        public int IntervalsPerMajorTic
        {
            get
            {
                return 1;
            }
        }

        public int Phase
        {
            get
            {
                return 0;
            }
        }

        public double Interval
        {
            get
            {
                return (double)(TlhAxis.Labels[1].Item1 - TlhAxis.Labels[0].Item1);
            }
        }

        public double FirstMajorTicValue
        {
            get
            {
                return (double)TlhAxis.Labels[0].Item1;
            }
        }

        public IList<Tic> Tics()
        {
            return TlhAxis.Labels.Select(label => new Tic { TicType = TicType.Major, Value = Convert.ToDouble(label.Item1) }).ToList();
        }
    }
}
