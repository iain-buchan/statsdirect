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
        private double dataMinX;
        private double dataMaxX;
        private double dataMinY;
        private double dataMaxY;

        private ScaleParameters scaleParameters;

        ///  <summary>
        ///  A default ChartDefinition with no values set.
        ///  </summary>
        ///  <remarks>TODO: This shouldn't be needed as even the one-liners should set most of their options in the definition.</remarks>
        private static ChartDefinition _Empty;

        // TRANSMISSINGCOMMENT: Method Empty
        public static ChartDefinition Empty()
        {
            return _Empty ?? (_Empty = new ChartDefinition());
        }


        public ChartDefinition()
        {
            YSeries = new List<Series>();
            XSeries = new List<Series>();
            dataMaxX = double.MinValue;
            dataMinX = double.MaxValue;
            dataMaxY = double.MinValue;
            dataMinY = double.MaxValue;
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


        ///  <summary>
        ///  The type of chart to be plotted.
        ///  </summary>
        public ChartType ChartType { get; set; }

        // TRANSMISSINGCOMMENT: Property ChartOptions
        public ChartOptions ChartOptions { get; set; }

        // TRANSMISSINGCOMMENT: Property DataMinX
        public double DataMinX
        {
            get
            {
                return dataMinX;
            }
        }

        // TRANSMISSINGCOMMENT: Property DataMaxX
        public double DataMaxX
        {
            get
            {
                return dataMaxX;
            }
        }

        // TRANSMISSINGCOMMENT: Property DataMinY
        public double DataMinY
        {
            get
            {
                return dataMinY;
            }
        }

        // TRANSMISSINGCOMMENT: Property DataMaxY
        public double DataMaxY
        {
            get
            {
                return dataMaxY;
            }
        }

        // TRANSMISSINGCOMMENT: Property XSeries
        public List<Series> XSeries { get; set; }

        // TRANSMISSINGCOMMENT: Property YSeries
        public List<Series> YSeries { get; set; }

        // TRANSMISSINGCOMMENT: Method AddXSeries
        public void AddXSeries(double[] data, string title)
        {
            DoubleSeries s = new DoubleSeries { Data = new double[data.Length] };
            Array.Copy(data, s.Data, data.Length);
            s.Title = title;
            XSeries.Add(s);
            CheckXSeriesData(s);
        }


        // TRANSMISSINGCOMMENT: Method AddXSeriesAt
        public void AddXSeriesAt(Series newSeries, int index)
        {
            while (XSeries.Count <= index)
            {
                XSeries.Add(null);
            }
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


        // TRANSMISSINGCOMMENT: Method AddYSeriesAt
        public void AddYSeriesAt(Series newSeries, int index)
        {
            while (YSeries.Count <= index)
            {
                YSeries.Add(null);
            }
            YSeries[index] = newSeries;
            CheckYSeriesData(newSeries.AsDoubleSeries);
        }


        // TRANSMISSINGCOMMENT: Property HasScaleParameters
        public bool HasScaleParameters
        {
            get
            {
                return scaleParameters != null;
            }
        }

        // TRANSMISSINGCOMMENT: Property ScaleParameters
        public virtual ScaleParameters ScaleParameters
        {
            get { return scaleParameters ?? (scaleParameters = GetScaleParameters()); }
            set
            {
                scaleParameters = value;
            }
        }

        private ScaleParameters GetScaleParameters()
        {
            using (ChartRenderer renderer = new ChartRenderer(this))
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
                    if (q < dataMinX)
                        dataMinX = q;
                    if (q > dataMaxX)
                        dataMaxX = q;
                }
            }
        }


        // TRANSMISSINGCOMMENT: Method CheckYSeriesData
        private void CheckYSeriesData(DoubleSeries S)
        {
            foreach (double q in S.Data)
            {
                if (q != Constant.MISSING)
                {
                    if (q < dataMinY)
                        dataMinY = q;
                    if (q > dataMaxY)
                        dataMaxY = q;
                }
            }
        }


        // TRANSMISSINGCOMMENT: Property FillerToUse
        public string FillerToUse
        {
            get
            {
                return "ChartOptions";
            }
        }

        // interface properties implemented by FillerToUse
        string IFillable.FillerToUse
        {
            get
            {
                return FillerToUse;
            }
        }
    }
}
