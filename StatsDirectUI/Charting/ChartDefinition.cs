using System;
using System.Collections.Generic;

using StatsDirect.Numerics;
using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    ///  <summary>
    ///  Holds all the data for a ChartRenderer to be able to render a chart with particular data and options.
    ///  </summary>
    ///  <remarks></remarks>
    public class ChartDefinition : IFillable
    {
        private ScaleParameters scaleParameters;

        public List<Series> XSeries { get; set; }
        public List<Series> YSeries { get; set; }

        public double DataMinX { get; private set; }
        public double DataMinGreaterThanZeroX { get; private set; }
        public double DataMaxX { get; private set; }
        public double DataMinY { get; private set; }
        public double DataMinGreaterThanZeroY { get; private set; }
        public double DataMaxY { get; private set; }

        ///  <summary>
        ///  The type of chart to be plotted.
        ///  </summary>
        public ChartType ChartType { get; set; }

        public ChartOptions ChartOptions { get; set; }

        public ChartDefinition()
        {
            YSeries = new List<Series>();
            XSeries = new List<Series>();
            DataMaxX = double.MinValue;
            DataMinX = double.MaxValue;
            DataMinGreaterThanZeroX = double.MaxValue;
            DataMaxY = double.MinValue;
            DataMinY = double.MaxValue;
            DataMinGreaterThanZeroY = double.MaxValue;
        }

        ///  <summary>
        ///  Returns a copy where it's safe to alter the order of the series or the options.
        ///  </summary>
        public ChartDefinition Clone()
        {
            ChartDefinition copy = new ChartDefinition
            {
                ChartOptions = ChartOptions.Clone(),
                ChartType = ChartType,
                ScaleParameters = ScaleParameters.Clone()
            };
            foreach (Series s in XSeries)
                copy.XSeries.Add(s);
            foreach (Series s in YSeries)
                copy.YSeries.Add(s);
            return copy;
        }

        public void AddXSeries(double[] data, string title)
        {
            DoubleSeries s = new DoubleSeries { Data = new double[data.Length] };
            Array.Copy(data, s.Data, data.Length);
            s.Title = title;
            XSeries.Add(s);
            CheckXSeriesData(s);
        }

        public void AddXSeriesAt(Series newSeries, int index)
        {
            while (XSeries.Count <= index)
                XSeries.Add(null);
            XSeries[index] = newSeries;
            CheckXSeriesData(newSeries.AsDoubleSeries);
        }

        public void AddYSeries(double[] data, string title)
        {
            DoubleSeries s = new DoubleSeries { Data = new double[data.Length] };
            Array.Copy(data, s.Data, data.Length);
            s.Title = title;
            YSeries.Add(s);
            CheckYSeriesData(s);
        }

        public void AddYSeriesAt(Series newSeries, int index)
        {
            while (YSeries.Count <= index)
                YSeries.Add(null);
            YSeries[index] = newSeries;
            CheckYSeriesData(newSeries.AsDoubleSeries);
        }

        public bool HasScaleParameters
        {
            get
            {
                return scaleParameters != null;
            }
        }

        public virtual ScaleParameters ScaleParameters
        {
            get { return scaleParameters ?? (scaleParameters = GetScaleParameters()); }
            set { scaleParameters = value; }
        }

        private ScaleParameters GetScaleParameters()
        {
            using (IChartRenderer renderer = ChartRendererFactory.ChartRendererFor(this))
            {
                return renderer.GetScaleParameters();
            }
        }

        private void CheckXSeriesData(DoubleSeries s)
        {
            foreach (double q in s.Data)
            {
                if (q != Constant.MISSING)
                {
                    if (q < DataMinX)
                        DataMinX = q;
                    if (q < DataMinGreaterThanZeroX && q > 0)
                        DataMinGreaterThanZeroX = q;
                    if (q > DataMaxX)
                        DataMaxX = q;
                }
            }
        }

        private void CheckYSeriesData(DoubleSeries s)
        {
            foreach (double q in s.Data)
            {
                if (q != Constant.MISSING)
                {
                    if (q < DataMinY)
                        DataMinY = q;
                    if (q < DataMinGreaterThanZeroY && q > 0)
                        DataMinGreaterThanZeroY = q;
                    if (q > DataMaxY)
                        DataMaxY = q;
                }
            }
        }

        public string FillerToUse
        {
            get
            {
                return "ChartOptions";
            }
        }
    }
}
