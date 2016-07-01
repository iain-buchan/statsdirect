using StatsDirect.Numerics;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace StatsDirect.Charting
{
    [Serializable]
    public class HistogramSeriesOptions
    {
        public string ChartTitle { get; set; }
        public string XAxisTitle { get; set; }
        public string YAxisTitle { get; set; }

        public BinsDescriptor BinsDescriptor { get; set; }

        /// <summary>
        /// Calculate minimum bin midpoint, midpoint interval and number of bins given the current state of the options.
        /// If PoolVariablesForBins is true, this combines the data for all the variables.
        /// If PoolVariablesForBins is false, this uses just the data from the series at seriesIndex.
        /// </summary>
        /// <param name="calculateBinCount">If true, force a full calculation of the number of bins.  If false, use the user-entered number of bins as a hint.</param>
        /// <param name="binsFromUser">The user-entered number of bins</param>
        /// <param name="series"> </param>
        public void Reset(bool calculateBinCount, int binsFromUser, Series series)
        {
            //  TODO: Set up the global minimum and maximum values based on the series

            //  Work out the values
            double min;
            double max;
            DoubleSeries s = series.AsDoubleSeries;
            min = s.Min;
            max = s.Max;
            BinsDescriptor descriptor = Calculate(s, binsFromUser, calculateBinCount);
            BinsDescriptor = descriptor;
        }

        ///  <summary>
        ///  
        ///  </summary>
        ///  <param name="series">Input data. All data will be pooled for the purposes of calculating minimum and maximum values.</param>
        ///  <param name="binsFromUser">A user-entered bin count.</param>
        ///  <param name="calculateBinCount">If false, use the user-entered bin count.  If true, calculate from scratch.</param>
        /// <remarks></remarks>
        public BinsDescriptor Calculate(Series series, int binsFromUser, bool calculateBinCount)
        {
            int actualRows;
            double[] nonMissingData = ExtractNonMissingDataAndSort(series, out actualRows);

            if (calculateBinCount || binsFromUser <= 1)
                return HistogramBinChooser.ChooseBins(nonMissingData, 0, actualRows);
            else
            {
                double[] edges = HistogramBinChooser.Linspace(nonMissingData[0], nonMissingData[actualRows - 1], binsFromUser);
                int[] counts = HistogramBinChooser.SortedHist(nonMissingData, edges);
                return new BinsDescriptor { Edges = edges, Counts = counts };
            }
        }

        public static double[] ExtractNonMissingDataAndSort(Series series, out int actualRows)
        {
            double[] data = series.AsDoubleSeries.Data;
            double[] nonMissingData = new double[data.Length];
            actualRows = 0;
            for (int c = 0; c < data.Length; c++)
                if (data[c] != Constant.MISSING)
                    nonMissingData[actualRows++] = data[c];
            //  By now, nonMissingData(0) to nonMissingData(actualRows - 1) contain the actual data for the longest row, with missing data excluded.
            Array.Sort(nonMissingData, 0, actualRows);
            return nonMissingData;
        }
    }

    [Serializable]
    public class HistogramOptions : GenericOptions
    {
        private bool showRelativeFrequencies;

        public bool OverlayNormalCurve { get; set; }
        ///  <summary>
        ///  Informational to the filler.  ASCII charts cannot overlay normals, so the option should not be given.
        ///  </summary>
        public bool IsAscii { get; set; }

        ///  <summary>
        ///  If true, all variables are pooled for bin calculations (i.e. there is a common X axis).
        ///  If false, bin calculations are per-variable.
        ///  </summary>
        public bool PoolVariablesForBins { get; set; }

        public HistogramOptions(bool useColour)
            : base(useColour)
        {
        }

        /// <summary>
        /// Calculate minimum bin midpoint, midpoint interval and number of bins given the current state of the options.
        /// If PoolVariablesForBins is true, this combines the data for all the variables.
        /// If PoolVariablesForBins is false, this uses just the data from the series at seriesIndex.
        /// </summary>
        /// <param name="calculateBinCount">If true, force a full calculation of the number of bins.  If false, use the user-entered number of bins as a hint.</param>
        /// <param name="binsFromUser">The user-entered number of bins</param>
        /// <param name="series"> </param>
        public void Reset(bool calculateBinCount, int binsFromUser, int seriesIndex, Series series)
        {
            HistoSeriesOptions[seriesIndex].Reset(calculateBinCount, binsFromUser, series);

            // Warn listeners that we've just changed our scale.
            if (null != ScaleChanged)
                ScaleChanged(this, EventArgs.Empty);
        }

        public bool ShowRelativeFrequencies
        {
            get
            {
                return showRelativeFrequencies;
            }
            set
            {
                bool changed = value != showRelativeFrequencies;
                showRelativeFrequencies = value;
                if (changed)
                {
                    if (null != ScaleChanged)
                        ScaleChanged(this, EventArgs.Empty);
                }
            }
        }

        //  Display options
        public int LineWidth { get; set; }
        public string AxisFontDescriptor { get; set; }

        public List<HistogramSeriesOptions> HistoSeriesOptions { get; set; }

        [field: NonSerialized]
        public event EventHandler ScaleChanged;

        public override bool UsesShowLegend
        {
            get
            {
                return false;
            }
        }

        public override bool UsesXAxisOptions
        {
            get { return false; }
        }

        public override ChartOptionType OptionType
        {
            get
            {
                return ChartOptionType.Histogram;
            }
        }

        public override bool ShowHistogramOptions
        {
            get { return true; }
        }

        public override bool ShowLegendIsRelevant
        {
            get { return HistoSeriesOptions.Count > 1; }
        }
    }
}
