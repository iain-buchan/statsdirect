using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace StatsDirect.Charting
{
    [Serializable]
    public class HistogramSeriesOptions
    {
        ///  <summary>
        ///  The smallest value in the data.  May be set by the filler if MinimumValue = MaximumValue during input.
        ///  </summary>
        public double MinimumValue;
        ///  <summary>
        ///  The largest value in the data.  May be set by the filler if MinimumValue = MaximumValue during input.
        ///  </summary>
        public double MaximumValue;
        ///  <summary>
        ///  The number of bins (= bars in the histogram)
        ///  </summary>
        public int Bins;

        public string ChartTitle;
        public string XAxisTitle;
        public string YAxisTitle;

        public double MinimumBinMidPoint;
        public double MidPointInterval;
    }

    [Serializable]
    public class HistogramOptions : GenericOptions
    {

        private bool _showRelativeFrequencies;

        public bool OverlayNormalCurve;
        ///  <summary>
        ///  Informational to the filler.  ASCII charts cannot overlay normals, so the option should not be given.
        ///  </summary>
        public bool IsAscii;

        ///  <summary>
        ///  If true, all variables are pooled for bin calculations (i.e. there is a common X axis).
        ///  If false, bin calculations are per-variable.
        ///  </summary>
        public bool PoolVariablesForBins;

        public HistogramOptions(bool UseColour)
            : base(UseColour)
        {

        }

        public bool ShowRelativeFrequencies
        {
            get
            {
                return _showRelativeFrequencies;
            }
            set
            {
                bool changed = value != _showRelativeFrequencies;
                _showRelativeFrequencies = value;
                if (changed)
                {
                    if (null != ScaleChanged)
                        ScaleChanged(this, EventArgs.Empty);
                }
            }
        }

        //  Display options
        public int LineWidth;
        public string AxisFontDescriptor;

        public List<HistogramSeriesOptions> HistoSeriesOptions;

        [field: NonSerialized]
        public event EventHandler ScaleChanged;

        public override bool UsesShowLegend
        {
            get
            {
                return false;
            }
        }

        public override OptionTypes OptionType
        {
            get
            {
                return OptionTypes.Histogram;
            }
        }

        ///  <summary>
        ///  Calculate minimum bin midpoint, midpoint interval and number of bins given the current state of the options.
        ///  If PoolVariablesForBins is true, this combines the data for all the variables.
        ///  If PoolVariablesForBins is false, this uses just the data from the series at seriesIndex.
        ///  </summary>
        ///  <param name="full">If true, force a full calculation of the number of bins.  If false, use the user-entered number of bins as a hint.</param>
        ///  <param name="binsFromUser">The user-entered number of bins</param>
        ///  <param name="seriesIndex">The index of the series on which calculations are to be made (if PoolVariablesForBins is false) and whose parameters are to be set (if setAllSeries is false).</param>
        ///  <param name="setAllSeries">If true, the calculation for this series is set for each series.</param>
        /// <param name="series"> </param>
        public void Reset(bool full, int binsFromUser, bool setAllSeries, int seriesIndex, List<Series> series)
        {
            //  TODO: Set up the global minimum and maximum values based on the series

            //  Work out the values
            double min;
            double max;
            double zmin = 0;
            double zint = 0;
            int bins;
            if (PoolVariablesForBins)
            {
                //  Set up initial minimum and maximum values
                min = double.MaxValue;
                max = double.MinValue;
                foreach (DoubleSeries s in series)
                {
                    if (s.Min < min)
                    {
                        min = s.Min;
                    }
                    if (s.Max > max)
                    {
                        max = s.Max;
                    }
                }
                Calculate(series, binsFromUser, full, ref min, ref max, ref zmin, ref zint, out bins);
            }
            else
            {
                DoubleSeries s = ((DoubleSeries)(series[seriesIndex]));
                List<Series> justOneSeries = new List<Series> { s };
                min = s.Min;
                max = s.Max;
                Calculate(justOneSeries, binsFromUser, full, ref min, ref max, ref zmin, ref zint, out bins);
            }

            //  Write the values
            if (setAllSeries)
            {
                for (int i = 0; i <= series.Count - 1; i++)
                {
                    HistogramSeriesOptions transTemp12 = HistoSeriesOptions[i];
                    transTemp12.MinimumValue = min;
                    transTemp12.MaximumValue = max;
                    transTemp12.MinimumBinMidPoint = zmin;
                    transTemp12.MidPointInterval = zint;
                    transTemp12.Bins = bins;

                }
            }
            else
            {
                HistogramSeriesOptions transTemp13 = HistoSeriesOptions[seriesIndex];
                transTemp13.MinimumValue = min;
                transTemp13.MaximumValue = max;
                transTemp13.MinimumBinMidPoint = zmin;
                transTemp13.MidPointInterval = zint;
                transTemp13.Bins = bins;

            }
            if (null != ScaleChanged) ScaleChanged(this, EventArgs.Empty);
        }


        // TRANSMISSINGCOMMENT: Method v_axis
        public void v_axis(ref double qmin, ref double qmax, int cm, ref double zmin, ref double zint)
        {
            if (cm > 0)
            {
                AxisScaler.Axis(ref qmin, ref qmax, cm, ref zmin, ref zint);
            }
            else
            {
                zmin = qmin;
                zint = qmax - qmin;
            }
        }


        ///  <summary>
        ///  
        ///  </summary>
        ///  <param name="oneOrMoreSeries">Input data. All data will be pooled for the purposes of calculating minimum and maximum values.</param>
        ///  <param name="binsFromUser">A user-entered bin count.</param>
        ///  <param name="full">If false, use the user-entered bin count.  If true, calculate from scratch.</param>
        ///  <param name="min">The lowest value in the input, minus 1 if there's only one value.</param>
        ///  <param name="max">The highest value in the input, plus 1 if there's only one value.</param>
        ///  <param name="zmin"></param>
        ///  <param name="zint"></param>
        /// <param name="outputBins"></param>
        /// <remarks></remarks>
        public void Calculate(List<Series> oneOrMoreSeries, int binsFromUser, bool full, ref double min, ref double max, ref double zmin, ref double zint, out int outputBins)
        {
            Debug.Assert(oneOrMoreSeries.Count > 0);

            //  Base the neat model on the longest column if there's more than one column
            int maxRows = int.MinValue;
            DoubleSeries longestSoFar = null;
            foreach (DoubleSeries s in oneOrMoreSeries)
            {
                if (s.Points > maxRows)
                {
                    maxRows = s.Points;
                    longestSoFar = s;
                }
            }
            Debug.Assert(null != longestSoFar);

            //  Assume there's at least one column, and therefore longestSoFar is never Nothing
            double[] xx = new double[maxRows + 1 ];
            int actualRows = 0;
            for (int C = 0; C <= maxRows - 1; C++)
            {
                if (longestSoFar.Data[C] != Numerics.Constant.MISSING)
                {
                    xx[actualRows] = longestSoFar.Data[C];
                    actualRows += 1;
                }
            }

            //  By now, xx(0) to xx(actualRows - 1) contain the actual data for the longest row, with missing data excluded.

            int mp = full ? 0 : binsFromUser;
            bool force = (mp == 0);
            if (force)
            {
                //  Work out how many bins we should have at maximum: between 7 and 20, depending on the number of samples
                int maxcm = Convert.ToInt32(Math.Pow(Convert.ToDouble(actualRows), 0.88) / 4.0);
                if (maxcm > 20)
                {
                    maxcm = 20;
                }
                if (maxcm < 7)
                {
                    maxcm = 7;
                }
                double mxx = 0.0;
                int mpp = 0;
                for (int cm = 1; cm <= maxcm; cm++)
                {
                    v_axis(ref min, ref max, cm - 1, ref zmin, ref zint);
                    int nmp = cm < 10 ? cm + 1 : cm - 1;
                    double nzmin = 0;
                    double nzint = 0;
                    v_axis(ref min, ref max, nmp - 1, ref nzmin, ref nzint);
                    string transTemp0 = nzint.ToString();
                    string transTemp1 = nzmin.ToString();
                    string transTemp2 = zint.ToString();
                    string transTemp3 = zmin.ToString();
                    if (  /* TRANSINFO: .NET Equivalent of Microsoft.VisualBasic NameSpace */ transTemp0.Length +  /* TRANSINFO: .NET Equivalent of Microsoft.VisualBasic NameSpace */ transTemp1.Length <  /* TRANSINFO: .NET Equivalent of Microsoft.VisualBasic NameSpace */ transTemp2.Length +  /* TRANSINFO: .NET Equivalent of Microsoft.VisualBasic NameSpace */ transTemp3.Length)
                    {
                        zint = nzint;
                        zmin = nzmin;
                        mp = nmp;
                    }
                    else
                    {
                        mp = cm;
                    }
                    int C2 = 0;
                    int clm = 0;
                    for (int C = 1; C <= mp; C++)
                    {
                        double high = zmin + (zint * Convert.ToDouble(C - 1)) + zint / 2.0;
                        int c1;
                        for (c1 = C2; c1 <= actualRows - 1; c1++)
                        {
                            if (xx[c1] > high)
                            {
                                break; /* TRANSWARNING: check that break is in correct scope */
                            }
                        }
                        int bin = c1 - C2;
                        if (bin > 0)
                        {
                            clm = clm + 1;
                        }
                        //  If bin > 1 Then clm = clm + 3
                        C2 = c1;
                    }
                    double qxx = Convert.ToDouble(clm);
                    if (qxx > mxx)
                    {
                        mxx = qxx;
                        mpp = mp;
                    }
                }
                mp = mpp;
            }

            //  Ensure the total number of bins is between 1 and 20
            if (mp < 1)
            {
                mp = 1;
            }
            if (mp > 20)
            {
                mp = 20;
            }

            v_axis(ref min, ref max, mp - 1, ref zmin, ref zint);
            if (force)
            {
                for (int C = 1; C <= 2; C++)
                {
                    int nmp = mp - C;
                    double nzmin = 0;
                    double nzint = 0;
                    if (nmp > 3)
                    {
                        v_axis(ref min, ref max, nmp - 1, ref nzmin, ref nzint);
                        string transTemp4 = nzint.ToString();
                        string transTemp5 = nzmin.ToString();
                        string transTemp6 = zint.ToString();
                        string transTemp7 = zmin.ToString();
                        if (  /* TRANSINFO: .NET Equivalent of Microsoft.VisualBasic NameSpace */ transTemp4.Length +  /* TRANSINFO: .NET Equivalent of Microsoft.VisualBasic NameSpace */ transTemp5.Length <  /* TRANSINFO: .NET Equivalent of Microsoft.VisualBasic NameSpace */ transTemp6.Length +  /* TRANSINFO: .NET Equivalent of Microsoft.VisualBasic NameSpace */ transTemp7.Length)
                        {
                            zint = nzint;
                            zmin = nzmin;
                            mp = nmp;
                            break; /* TRANSWARNING: check that break is in correct scope */
                        }
                    }
                    nmp = mp + C;
                    if (nmp <= 20)
                    {
                        v_axis(ref min, ref max, nmp - 1, ref nzmin, ref nzint);
                        string transTemp8 = nzint.ToString();
                        string transTemp9 = nzmin.ToString();
                        string transTemp10 = zint.ToString();
                        string transTemp11 = zmin.ToString();
                        if (  /* TRANSINFO: .NET Equivalent of Microsoft.VisualBasic NameSpace */ transTemp8.Length +  /* TRANSINFO: .NET Equivalent of Microsoft.VisualBasic NameSpace */ transTemp9.Length <  /* TRANSINFO: .NET Equivalent of Microsoft.VisualBasic NameSpace */ transTemp10.Length +  /* TRANSINFO: .NET Equivalent of Microsoft.VisualBasic NameSpace */ transTemp11.Length)
                        {
                            zint = nzint;
                            zmin = nzmin;
                            mp = nmp;
                            break; /* TRANSWARNING: check that break is in correct scope */
                        }
                    }
                }

                // use up to last occupied bin
                int C2 = actualRows;
                for (int C = mp; C >= 1; C--)
                {
                    double bin_left = zmin + (zint * Convert.ToDouble(C - 1L)) - zint / 2.0;
                    bool OK = false;
                    for (int C1 = C2; C1 >= 1; C1--)
                    {
                        if (xx[C1] > bin_left)
                        {
                            OK = true;
                            C2 = C1;
                            break; /* TRANSWARNING: check that break is in correct scope */
                        }
                    }
                    if (!(OK))
                    {
                        mp = mp - 1;
                    }
                    else
                    {
                        break; /* TRANSWARNING: check that break is in correct scope */
                    }
                }

                // use from first occupied bin
                int budge = 0; //  TODO: This doesn't seem appropriate - what have I missed?
                C2 = 1;
                for (int C = 1; C <= mp; C++)
                {
                    double bin_right = zmin + (zint * Convert.ToDouble(C - 1L)) + zint / 2.0;
                    bool OK = false;
                    for (int C1 = C2; C1 <= actualRows; C1++)
                    {
                        if (xx[C1] <= bin_right)
                        {
                            OK = true;
                            C2 = C1 + 1;
                            break; /* TRANSWARNING: check that break is in correct scope */
                        }
                    }
                    if (!(OK))
                    {
                        budge += 1;
                        mp -= 1;
                    }
                    else
                    {
                        break; /* TRANSWARNING: check that break is in correct scope */
                    }
                }
                zmin = zmin + zint * budge;
            }
            outputBins = mp;
        }


        // TRANSMISSINGCOMMENT: Property ShowHistogramOptions
        public override bool ShowHistogramOptions
        {
            get
            {
                return true;
            }
        }

        // TRANSMISSINGCOMMENT: Property ShowLegendIsRelevant
        public override bool ShowLegendIsRelevant
        {
            get
            {
                return HistoSeriesOptions.Count > 1;
            }
        }
    }


}
