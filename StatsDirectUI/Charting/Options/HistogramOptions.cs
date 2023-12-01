using System;
using System.Collections.Generic;

namespace StatsDirect.Charting.Options
{
    [Serializable]
    public class HistogramOptions : AbstractGenericOptions
    {
        private bool showRelativeFrequencies;

        //  Display options
        public int LineWidth { get; }
        public IReadOnlyList<HistogramSeriesOptions> HistogramSeriesOptions { get; }
        public bool OverlayNormalCurve { get; }
        ///  <summary>
        ///  Informational to the filler.  ASCII charts cannot overlay normals, so the option should not be given.
        ///  </summary>
        public bool IsAscii { get; }

        ///  <summary>
        ///  If true, all variables are pooled for bin calculations (i.e. there is a common X axis).
        ///  If false, bin calculations are per-variable.
        ///  </summary>
        public bool PoolVariablesForBins { get; }

        public BinChoiceMethod BinChoiceMethod { get; }

        public HistogramOptions(IChartPreferences chartPreferences,
            IReadOnlyList<HistogramSeriesOptions> histogramSeriesOptions,
            FontDescriptor? axisLabelFontDescriptor = default,
            float? axisLineThickness = default,
            FontDescriptor? axisTitleFontDescriptor = default,
            BinChoiceMethod? binChoiceMethod = default,
            bool? isAscii = default,
            FontDescriptor? legendFontDescriptor = default,
            int? lineWidth = default,
            ChartOrientation? orientation = default,
            IReadOnlyList<string?>? seriesTitles = default,
            bool? shouldAutoscale = default,
            bool? shouldBoxAxes = default,
            bool? showLegend = default,
            string? title = default,
            FontDescriptor? titleFontDescriptor = default,
            bool? useColour = default,
            string? xAxisTitle = default,
            string? yAxisTitle = default)
            : base(chartPreferences,
                  axisLabelFontDescriptor,
                  axisLineThickness,
                  axisTitleFontDescriptor,
                  legendFontDescriptor,
                  orientation,
                  seriesTitles,
                  shouldAutoscale,
                  shouldBoxAxes,
                  showLegend,
                  title,
                  titleFontDescriptor,
                  useColour,
                  xAxisTitle,
                  yAxisTitle)
        {
            BinChoiceMethod = binChoiceMethod ?? BinChoiceMethod.Doane;
            HistogramSeriesOptions = histogramSeriesOptions;
            IsAscii = isAscii ?? false;
            LineWidth = lineWidth ?? 1;
        }

        /// <summary>
        /// Calculate minimum bin midpoint, midpoint interval and number of bins given the current state of the options.
        /// If PoolVariablesForBins is true, this combines the data for all the variables.
        /// If PoolVariablesForBins is false, this uses just the data from the series at seriesIndex.
        /// </summary>
        /// <param name="calculateBinCount">If true, force a full calculation of the number of bins.  If false, use the user-entered number of bins as a hint.</param>
        /// <param name="binsFromUser">The user-entered number of bins</param>
        /// <param name="series"> </param>
        public void Reset(bool calculateBinCount, int binsFromUser, int seriesIndex, DoubleSeries series)
        {
            HistogramSeriesOptions[seriesIndex].Reset(calculateBinCount, binsFromUser, series, BinChoiceMethod);

            // Warn listeners that we've just changed our scale.
            ScaleChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool ShowRelativeFrequencies
        {
            get => showRelativeFrequencies;
            set
            {
                bool changed = value != showRelativeFrequencies;
                showRelativeFrequencies = value;
                if (changed)
                    ScaleChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        [field: NonSerialized]
        public event EventHandler? ScaleChanged;

        public override bool UsesShowLegend => false;
        public override bool UsesXAxisOptions => false;

        public override bool ShowLegendIsRelevant => HistogramSeriesOptions.Count > 1;

        public override void Accept(IChartOptionVisitor visitor) => visitor.Visit(this);
    }
}
