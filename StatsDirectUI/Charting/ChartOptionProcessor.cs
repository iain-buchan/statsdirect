using System;
using System.Collections.Generic;
using System.Diagnostics;
using StatsDirect.Charting.Options;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.Charting
{
    public class ChartDefinitionProcessor
    {
        private IChartPreferences ChartPreferences { get; }
        private ISdPreferences SdPreferences { get; }

        public ChartDefinitionProcessor(IChartPreferences chartPreferences, ISdPreferences sdPreferences)
        {
            ChartPreferences = chartPreferences;
            SdPreferences = sdPreferences;
        }

        public ChartDefinition Preprocess(ChartStep step, ParameterBag parameters, IReadOnlyList<ISeries>? xSeries, IReadOnlyList<ISeries>? ySeries, string? dataName)
        {
            // Chart options
            // TODO: This is very poor placement of this logic.  It's an unpleasant mash of setting options (some of which should be defaults), data preparation and mapping from values in particular operations.  How much of this should be moved out to the XML?
            return step.ChartType switch
            {
                ChartType.AgreementPair => PreprocessAgreement(step, parameters),
                ChartType.Bar or ChartType.StackedBar or ChartType.StackedBar100Percent => PreprocessBar(step, parameters, ySeries, dataName),
                ChartType.BiasMA => PreprocessBiasMA(),
                ChartType.BoxWhisker => PreprocessBoxWhiskerOptions(step, xSeries, ySeries, dataName),
                ChartType.Control => PreprocessControlOptions(xSeries, ySeries, dataName),
                ChartType.ErrorBar => PreprocessErrorBarOptions(parameters, dataName),
                ChartType.Forest => PreprocessForest(parameters, dataName),
                ChartType.Gini => PreprocessGini(xSeries, ySeries, dataName),
                ChartType.Histogram => PreprocessHistogram(step, xSeries, ySeries),
                ChartType.Ladder => PreprocessLadder(xSeries, ySeries, dataName),
                ChartType.LineXY => PreprocessLineXY(xSeries, ySeries, dataName),
                ChartType.LinearRegression => PreprocessLinearRegression(step, parameters, xSeries, ySeries),
                ChartType.Normal => PreprocessNormal(parameters, dataName, xSeries, ySeries),
                ChartType.Pyramid => PreprocessPyramid(parameters, dataName),
                ChartType.ScatterXY => PreprocessScatterXY(step, xSeries, ySeries, dataName),
                ChartType.ROC => PreprocessRoc(parameters),
                ChartType.Spread => PreprocessSpread(xSeries, ySeries, dataName),
                ChartType.Survival => PreprocessSurvival(parameters, dataName),
                _ => throw new ArgumentOutOfRangeException(nameof(step), step, "step.ChartType: Not all types can be plotted yet"),
            };
        }

        private ChartDefinition PreprocessBiasMA()
        {
            // Nothing required
            return null;
        }

        private ChartDefinition PreprocessSpread(IReadOnlyList<ISeries>? xSeries, IReadOnlyList<ISeries>? ySeries, string? dataName)
        {
            // If only X series have been passed in, we're vertical.  If only Y, we're horizontal.  If both or neither, we can't plot.
            if (xSeries is null || ySeries is null || 0 == xSeries.Count && 0 == ySeries.Count)
                throw new ArgumentException("Must have at least one series to plot a spread plot");
            if (xSeries.Count > 0 && ySeries.Count > 0)
                throw new ArgumentException("Cannot plot a spread plot with both X and Y series");
            IReadOnlyList<ISeries> seriesToUse = ySeries.Count > 0
                ? ySeries
                : xSeries;

            string?[] seriesTitles = new string[seriesToUse.Count];
            for (int i = 0; i < seriesToUse.Count; i++)
                seriesTitles[i] = seriesToUse[i].Title;
            return new ChartDefinition(
                ChartType.Spread,
                new SpreadOptions(ChartPreferences,
                    seriesTitles: seriesTitles,
                    shouldAutoscale: !ChartPreferences.RequestScaleLimits,
                    title: dataName is null
                        ? "Spread plot"
                        : $"Spread plot from {dataName}"
                ),
                xSeries,
                ySeries
            );
        }

        private ChartDefinition PreprocessRoc(ParameterBag parameters)
        {
            int seriesCount = parameters.GetNotNullValueOrThrow<int>("series-count");
            // Series: First present...
            DataFrame frame = parameters.GetNotNullValueOrThrow<DataFrame>("P");
            DoubleSeries[] xSeries = new DoubleSeries[seriesCount];
            for (int v = 0; v < frame.VariableCount; v++)
                xSeries[v] = VariableToSeries(frame.VariableOrThrow<DoubleVariable>(v));
            // ... then absent
            frame = parameters.GetNotNullValueOrThrow<DataFrame>("A");
            DoubleSeries[] ySeries = new DoubleSeries[seriesCount];
            for (int v = 0; v < frame.VariableCount; v++)
                ySeries[v] = VariableToSeries(frame.VariableOrThrow<DoubleVariable>(v));
            string? dataName = frame.Name;
            double pmn = 1;
            double amn = 1;
            for (int c = 0; c < xSeries.Length; c++)
            {
                DoubleSeries xs = xSeries[c];
                DoubleSeries ys = ySeries[c];
                pmn = xs.Sum / xs.Points;
                amn = ys.Sum / ys.Points;
            }

            string[] seriesTitles = new string[seriesCount];
            for (int i = 0; i < seriesCount; i++)
                seriesTitles[i] = $"{xSeries[i].Title} (+ve), {ySeries[i].Title} (-ve)";
            return new ChartDefinition(
                ChartType.ROC,
                new ROCOptions(ChartPreferences,
                    xSeries, 
                    comparison: pmn > amn
                        ? Comparison.GreaterEqual
                        : Comparison.LessEqual,
                    gamma: parameters.TryGetValue("GAMMA", out double? gamma)
                        ? gamma.Value
                        : SdPreferences.DefaultConfidenceInterval,
                    seriesTitles: seriesTitles,
                    shouldAutoscale: !ChartPreferences.RequestScaleLimits,
                    showCutOffCalculator: true,
                    showOptimumCutOff: true,
                    title: dataName is null
                        ? "ROC plot"
                        : $"ROC plot from {dataName}",
                    weight: 1.0
                ),
                xSeries,
                ySeries
            );
        }

        private ChartDefinition PreprocessScatterXY(ChartStep step, IReadOnlyList<ISeries> xSeries, IReadOnlyList<ISeries> ySeries, string? dataName)
        {
            string?[] seriesTitles = new string?[xSeries.Count];
            for (int i = 0; i < xSeries.Count; i++)
                seriesTitles[i] = xSeries[i].Title;
            return new(
                ChartType.ScatterXY,
                new ScatterXYOptions(ChartPreferences, xSeries,
                    isAscii: step.IsAscii,
                    shouldAutoscale: !ChartPreferences.RequestScaleLimits,
                    title: string.IsNullOrWhiteSpace(step.ChartTitle)
                        ? dataName is null
                            ? "Scatter plot"
                            : $"Scatter plot from {dataName}"
                        : step.ChartTitle,
                    yAxisTitle: ySeries[0].Title,
                    xAxisTitle: xSeries[0].Title,
                    seriesTitles: seriesTitles
                ),
                xSeries,
                ySeries
            );
        }

        private ChartDefinition PreprocessPyramid(ParameterBag parameters, string? dataName)
        {
            if (!(parameters.TryGetValue("male", out DataFrame? maleFrame) && maleFrame is not null && maleFrame.VariableCount == 1 && maleFrame.Variables[0] is DoubleVariable maleVariable))
                throw new Exception("'male' pyramid chart parameter does not contain a single DoubleVariable");
            DoubleSeries maleOrOnlySeries = VariableToSeries(maleVariable);
            DoubleSeries? femaleSeries = null;
            if (parameters.TryGetValue("female", out DataFrame? femaleFrame))
            {
                if (!(femaleFrame is not null && femaleFrame.VariableCount == 1 && femaleFrame.Variables[0] is DoubleVariable femaleVariable))
                    throw new Exception("'female' pyramid chart parameter is present but does not contain a single DoubleVariable");
                femaleSeries = VariableToSeries(femaleVariable);
            }
            StringSeries? labelsSeries = null;
            if (parameters.TryGetValue("labels", out DataFrame? labelsFrame))
            {
                if (!(labelsFrame is not null && labelsFrame.VariableCount == 1 && labelsFrame.Variables[0] is StringVariable labelsVariable))
                    throw new Exception("'labels' pyramid chart parameter is present but does not contain a single StringVariable");
                labelsSeries = VariableToSeries(labelsVariable);
            }

            return new ChartDefinition(
                ChartType.Pyramid,
                new PyramidOptions(
                    ChartPreferences,
                    femaleSeries: femaleSeries,
                    labelSeries: labelsSeries,
                    maleOrOnlySeries: maleOrOnlySeries,
                    shouldAutoscale: !ChartPreferences.RequestScaleLimits,
                    title: dataName is null
                        ? "Population pyramid"
                        : $"Population pyramid from {dataName}"
                ),
                Array.Empty<ISeries>(),
                Array.Empty<ISeries>()
            );
        }

        private ChartDefinition PreprocessNormal(ParameterBag parameters, string? dataName, IReadOnlyList<ISeries> xSeries, IReadOnlyList<ISeries> ySeries)
        {
            return new(
                ChartType.Normal,
                new NormalOptions(ChartPreferences,
                    method: parameters.TryGetValue("ScoreMethod", out string? scoreMethodString)
                        ? (NormalOptions.ScoreMethod)Parsing.Cint_Txt(scoreMethodString)
                        : NormalOptions.ScoreMethod.VanDerWaerden,
                    shouldAutoscale: !ChartPreferences.RequestScaleLimits,
                    title: dataName is null
                        ? "Normal plot"
                        : $"Normal plot from {dataName}"
                ),
                xSeries,
                ySeries
            );
        }

        private ChartDefinition PreprocessLinearRegression(ChartStep step, ParameterBag parameters, IReadOnlyList<ISeries> xSeries, IReadOnlyList<ISeries> ySeries)
        {
            if (!(parameters.TryGetValue("chartIsFullWidth", out bool? chartIsFullWidth) && chartIsFullWidth.HasValue
                && parameters.TryGetValue("interceptValue", out double? interceptValue) && interceptValue.HasValue
                && parameters.TryGetValue("mdnValue", out double? mdnValue) && mdnValue.HasValue
                && parameters.TryGetValue("xtitle", out string? xtitle)
                && parameters.TryGetValue("ytitle", out string? ytitle)
                ))
                throw new Exception("Linear Regression chart needs all of parameters 'chartIsFullWidth', 'interceptValue', 'mdnValue', 'xtitle' and 'ytitle'; at least one is missing");
            return new(
                ChartType.LinearRegression,
                new LinearRegressionOptions(ChartPreferences,
                    fullWidth: chartIsFullWidth.Value,
                    intercept: interceptValue.Value,
                    slope: mdnValue.Value,
                    title: step.ChartTitle,
                    xAxisTitle: xtitle,
                    yAxisTitle: ytitle
                ),
                xSeries,
                ySeries
            );
        }

        private ChartDefinition PreprocessLineXY(IReadOnlyList<ISeries> xSeries, IReadOnlyList<ISeries> ySeries, string? dataName)
        {
            string?[] seriesTitles = new string?[xSeries.Count];
            for (int i = 0; i < xSeries.Count; i++)
                seriesTitles[i] = xSeries[i].Title;
            return new(
                ChartType.ScatterXY,
                new ScatterXYOptions(ChartPreferences,
                    joinMarkersWithLines: true,
                    plotMarkers: true,
                    seriesTitles: seriesTitles,
                    shouldAutoscale: !ChartPreferences.RequestScaleLimits,
                    title: dataName is null
                        ? "Line plot"
                        : "Line plot from " + dataName,
                    xSeries: xSeries,
                    xAxisTitle: xSeries[0].Title,
                    yAxisTitle: ySeries[0].Title
                ),
                xSeries,
                ySeries
            );
        }

        private ChartDefinition PreprocessLadder(IReadOnlyList<ISeries> xSeries, IReadOnlyList<ISeries> ySeries, string? dataName)
        {
            // We need exactly 2 Y series
            if (2 != ySeries.Count || 0 != xSeries.Count)
                throw new ArgumentException("Must have exactly two Y series for a ladder plot");

            string?[] seriesTitles = new string?[ySeries.Count];
            for (int i = 0; i < ySeries.Count; i++)
                seriesTitles[i] = ySeries[i].Title;

            return new(
                ChartType.Ladder,
                new LadderOptions(ChartPreferences,
                    shouldAutoscale: !ChartPreferences.RequestScaleLimits,
                    title: dataName is null
                        ? "Ladder plot"
                        : "Ladder plot from " + dataName,
                    seriesTitles: seriesTitles
                ),
                xSeries,
                ySeries
            );
        }

        private ChartDefinition PreprocessForest(ParameterBag parameters, string? dataName)
        {
            const int DEFAULT_GROUP_SIZE = 10;

            double[] oddsRatioLcis = parameters.GetVariableOrThrow<DoubleVariable>("lci", 0).Data;
            DoubleVariable oddsRatiosVariable = parameters.GetVariableOrThrow<DoubleVariable>("odds", 0);
            double[] oddsRatioUcis = parameters.GetVariableOrThrow<DoubleVariable>("uci", 0).Data;

            double[] oddsRatios = oddsRatiosVariable.Data;
            int k = oddsRatios.Length;

            double[] groupSizes;
            if (parameters.TryGetVariable("gn", 0, out DoubleVariable? gnVariable))
            {
                groupSizes = gnVariable.Data;
            }
            else
            {
                groupSizes = new double[k];
                Array.Fill(groupSizes, DEFAULT_GROUP_SIZE);
            }
            double[]? pg = default;
            if (parameters.TryGetVariable("pg", 0, out DoubleVariable? pgVariable))
                pg = pgVariable.Data;
            string[] titles = new string[k];
            if (parameters.TryGetVariable("title", 0, out StringVariable? titleVariable))
            {
                string?[] titleData = titleVariable.Data;
                for (int i = 0; i < k; i++)
                {
                    string? candidate = titleData[i];
                    titles[i] = string.IsNullOrWhiteSpace(candidate)
                        ? $"stratum {i + 1}"
                        : candidate;
                }
            }
            else
            {
                for (int i = 0; i < k; i++)
                    titles[i] = $"stratum {i + 1}";
            }

            // Sort out candidate decimal places
            double absmin = double.MaxValue;
            for (int i = 0; i < k; i++)
            {
                if (Math.Abs(oddsRatios[i]) < absmin && oddsRatios[i] != 0.0)
                    absmin = Math.Abs(oddsRatios[i]);
                if (Math.Abs(oddsRatioLcis[i]) < absmin && oddsRatioLcis[i] != 0.0)
                    absmin = Math.Abs(oddsRatioLcis[i]);
                if (Math.Abs(oddsRatioUcis[i]) < absmin && oddsRatioUcis[i] != 0.0)
                    absmin = Math.Abs(oddsRatioUcis[i]);
            }
            int decpm = 2;
            try
            {
                if (absmin < Math.Pow(10, -decpm) && absmin != 0D)
                {
                    decpm = 3;
                    if (absmin < Math.Pow(10, -decpm) && absmin != 0D)
                        decpm = 4;
                }
            }
            catch
            {
                // Do nothing; keep decpm=2
            }

            ForestOptions fOptions = new(ChartPreferences,
                cco: 0.95,
                effectSizeAndIntervalDecimalPlaces: decpm,
                groupSizes: groupSizes,
                k: k,
                oddsRatioLcis: oddsRatioLcis,
                oddsRatios: oddsRatios,
                oddsRatioUcis: oddsRatioUcis,
                pg: pg,
                shouldAutoscale: !ChartPreferences.RequestScaleLimits,
                title: dataName is null
                    ? "Forest plot"
                    : $"Forest plot from {dataName}",
                titles: titles,
                xAxisTitle: $"{oddsRatiosVariable.Title} (95% confidence interval)"
            );
            return new(
                ChartType.Forest,
                fOptions,
                Array.Empty<ISeries>(),
                Array.Empty<ISeries>()
            );
        }

        private ChartDefinition PreprocessGini(IReadOnlyList<ISeries> xSeries, IReadOnlyList<ISeries> ySeries, string? dataName)
        {
            // We need exactly one of each series
            if (1 != ySeries.Count || 1 != xSeries.Count)
                throw new ArgumentException("Must have exactly one X series and one Y series for a Gini chart");

            return new(
                ChartType.Gini,
                new GiniOptions(ChartPreferences,
                    shouldAutoscale: !ChartPreferences.RequestScaleLimits,
                    title: dataName is null
                        ? "Lorenz plot"
                        : $"Lorenz plot from {dataName}"
                ),
                xSeries,
                ySeries
            );
        }

        private ChartDefinition PreprocessHistogram(ChartStep step, IReadOnlyList<ISeries> xSeries, IReadOnlyList<ISeries> ySeries)
        {
            IReadOnlyList<ISeries> series = xSeries.Count > 0
                ? xSeries
                : ySeries;
            List<HistogramSeriesOptions> histoSeriesOptions = new(series.Count);
            // Series
            foreach (ISeries t in series)
                histoSeriesOptions.Add(new()
                {
                    ChartTitle = $"Distribution of {t.Title}",
                    YAxisTitle = "Counts",
                    XAxisTitle = $"Mid-points for {t.Title}"
                });
            return new(
                ChartType.Histogram,
                new HistogramOptions(ChartPreferences,
                    binChoiceMethod: step.IsAscii
                        ? BinChoiceMethod.OldStatsDirect
                        : BinChoiceMethod.Doane,
                    histogramSeriesOptions: histoSeriesOptions,
                    isAscii: step.IsAscii,
                    lineWidth: 2
                ),
                xSeries,
                ySeries
            );
        }

        private ChartDefinition PreprocessErrorBarOptions(ParameterBag parameters, string? dataName)
        {
            DataFrame xdatFrame = parameters.GetNotNullValueOrThrow<DataFrame>("xdat");
            DataFrame ydatFrame = parameters.GetNotNullValueOrThrow<DataFrame>("ydat");
            DataFrame ydatlFrame = parameters.GetNotNullValueOrThrow<DataFrame>("ydatl");
            DataFrame ydatuFrame = parameters.GetNotNullValueOrThrow<DataFrame>("ydatu");

            List<MultiDoubleSeries> allSeries = new(xdatFrame.VariableCount);

            for (int sIndex = 0; sIndex < xdatFrame.VariableCount; sIndex++)
            {
                DoubleVariable xdat = (DoubleVariable)xdatFrame.Variables[sIndex];
                DoubleVariable ydat = (DoubleVariable)ydatFrame.Variables[sIndex];
                DoubleVariable ydatl = (DoubleVariable)ydatlFrame.Variables[sIndex];
                DoubleVariable ydatu = (DoubleVariable)ydatuFrame.Variables[sIndex];
                string seriesTitle =
                    string.IsNullOrWhiteSpace(ydat.Title)
                        ? "Series " + (sIndex + 1)
                        : ydat.Title;
                DoubleArraysAndBooleans noMissings = Numerics.Utilities.RemoveMissingRows(new[] { xdat.Data, ydat.Data, ydatl.Data, ydatu.Data }, 0, xdat.Length, 0);
                MultiDoublePoint[] data = new MultiDoublePoint[noMissings.ArraysWithMissingRowsRemoved[0].Length];
                for (int i = 0; i < noMissings.ArraysWithMissingRowsRemoved[0].Length; i++)
                {
                    MultiDoublePoint p = new() { X = noMissings.ArraysWithMissingRowsRemoved[0][i] };
                    // Ordinate
                    // Y values for error bars: [0] is centre, [1] is lower bound, [2] is upper bound.
                    p.set_Y(0, noMissings.ArraysWithMissingRowsRemoved[1][i]);
                    p.set_Y(1, noMissings.ArraysWithMissingRowsRemoved[2][i]);
                    p.set_Y(2, noMissings.ArraysWithMissingRowsRemoved[3][i]);
                    data[i] = p;
                }
                allSeries.Add(new MultiDoubleSeries { Title = seriesTitle, Data = data });
            }

            string?[] seriesTitles = new string?[ydatFrame.VariableCount];
            for (int i = 0; i < ydatFrame.VariableCount; i++)
                seriesTitles[i] = string.IsNullOrEmpty(ydatFrame.Variables[i].Title)
                        ? $"Series {(i + 1)}"
                        : ydatFrame.Variables[i].Title;
            return new(
                ChartType.ErrorBar,
                new ErrorBarOptions(ChartPreferences,
                    allSeries, 
                    seriesTitles: seriesTitles,
                    shouldAutoscale: !ChartPreferences.RequestScaleLimits,
                    title: dataName is null
                        ? "Error bar plot"
                        : $"Error bar plot plot from {dataName}",
                    xAxisTitle: xdatFrame.Variables[0].Title,
                    yAxisTitle: ydatFrame.Variables[0].Title
                ),
                Array.Empty<ISeries>(),
                Array.Empty<ISeries>()
            );
        }

        private ChartDefinition PreprocessControlOptions(IReadOnlyList<ISeries> xSeries, IReadOnlyList<ISeries> ySeries, string? dataName)
        {
            // We need exactly one of each series
            if (1 != ySeries.Count || 1 != xSeries.Count)
                throw new ArgumentException("Must have exactly one X series and one Y series for a control chart");

            // This is a duplicate of the top analysis in PlotControl.
            DoubleSeries? xs0 = xSeries[0] as DoubleSeries;
            DoubleSeries? ys0 = ySeries[0] as DoubleSeries;
            Debug.Assert(xs0 is not null && ys0 is not null);
            int rows = xs0.Points;
            double[] xdat = new double[rows];
            double[] ydat = new double[rows];

            int ctr = 0;
            double[] ySeriesData = ys0.Data;
            double[] xSeriesData = xs0.Data;
            for (int r = 0; r < rows; r++)
            {
                if (ySeriesData[r] != Constant.MISSING && xSeriesData[r] != Constant.MISSING)
                {
                    xdat[ctr] = xSeriesData[r];
                    ydat[ctr] = ySeriesData[r];
                    ctr++;
                }
            }
            rows = ctr;

            return new(
                ChartType.Control,
                new ControlOptions(ChartPreferences,
                    observationsToUse: rows,
                    rightHandDecimalPlaces: 3,
                    shouldBoxAxes: ChartPreferences.BoxAxes,
                    shouldAutoscale: !ChartPreferences.RequestScaleLimits,
                    title: dataName is null
                        ? "Control chart"
                        : $"Control chart from {dataName}",
                    use1Sd: true,
                    use2Sd: true,
                    use3Sd: true,
                    useMean: true,
                    xAxisTitle: xSeries[0].Title,
                    yAxisTitle: ySeries[0].Title
                ),
                xSeries,
                ySeries
            );
        }

        private ChartDefinition PreprocessBoxWhiskerOptions(ChartStep step, IReadOnlyList<ISeries> xSeries, IReadOnlyList<ISeries> ySeries, string? dataName)
        {
            // If only X series have been passed in, we're vertical.  If only Y, we're horizontal.  If both or neither, we can't plot.
            if (0 == xSeries.Count && 0 == ySeries.Count)
                throw new ArgumentException("Must have at least one series to plot a box+whisker plot");
            if (xSeries.Count > 0 && ySeries.Count > 0)
                throw new ArgumentException("Cannot plot a box+whisker plot with both X and Y series");
            IReadOnlyList<ISeries> seriesToUse = ySeries.Count > 0
                ? ySeries
                : xSeries;

            string?[] seriesTitles = new string[seriesToUse.Count];
            for (int i = 0; i < seriesToUse.Count; i++)
                seriesTitles[i] = seriesToUse[i].Title;
            return new(
                ChartType.BoxWhisker,
                new BoxWhiskerOptions(ChartPreferences,
                    isAscii: step.IsAscii,
                    markMeanAndMedian: false,
                    orientation: ySeries.Count > 0
                        ? ChartOrientation.Horizontal
                        : ChartOrientation.Vertical,
                    seriesTitles: seriesTitles,
                    title: dataName is null
                        ? "Box & whisker plot"
                        : $"Box & whisker plot from {dataName}",
                    useInnerFence: true,
                    useOuterFence: true
                ),
                xSeries,
                ySeries
            );
        }

        private ChartDefinition PreprocessBar(ChartStep step, ParameterBag parameters, IReadOnlyList<ISeries> ySeries, string? dataName)
        {
            bool isStacked = ChartType.StackedBar == step.ChartType || ChartType.StackedBar100Percent == step.ChartType;
            StringVariable labelsVariable = parameters.GetVariableOrThrow<StringVariable>("labels", 0);
            string?[] seriesTitles = new string[labelsVariable.Length];
            for (int i = 0; i < labelsVariable.Length; i++)
                seriesTitles[i] = labelsVariable.Data[i];
            bool showLegend = 1 < ySeries.Count;

            return new ChartDefinition(step.ChartType,
                new BarOptions(ChartPreferences,
                    something,
                    ySeries,
                    maxBarWidth: isStacked
                        ? 0.6 // Default 60% bar width
                        : 0.6 / ySeries.Count,
                    orientation: ChartOrientation.Vertical,
                    seriesTitles: seriesTitles,
                    shouldAutoscale: !ChartPreferences.RequestScaleLimits,
                    showLegend: showLegend,
                    stacked: isStacked,
                    stacked100Percent: ChartType.StackedBar100Percent == step.ChartType,
                    title: dataName is null
                        ? "Bar chart"
                        : "Bar chart from " + dataName,
                    yAxisTitle: showLegend
                        ? null
                        : ySeries[0].Title
                ),
                Array.Empty<ISeries>(),
                ySeries);
        }

        private ChartDefinition PreprocessAgreement(ChartStep step, ParameterBag parameters)
        {
            double[] av = parameters.GetVariableOrThrow<DoubleVariable>("av", 0).Data;
            double[] mxd = parameters.GetVariableOrThrow<DoubleVariable>("mxd", 0).Data;
            double lla = parameters.GetValueOrThrow<double>("lla");
            double mean = parameters.GetValueOrThrow<double>("mean");
            double p0 = parameters.GetValueOrThrow<double>("P0");
            double ula = parameters.GetValueOrThrow<double>("ula");
            return new(
                step.ChartType,
                new AgreementOptions(ChartPreferences,
                    av: av,
                    lla: lla,
                    mean: mean,
                    mxd: mxd,
                    p0: p0,
                    shouldAutoscale: !ChartPreferences.RequestScaleLimits,
                    title: step.ChartTitle,
                    ula: ula
                ),
                null,
                null);
        }

        private ChartDefinition PreprocessSurvival(ParameterBag parameters, string? dataName)
        {
            if (!(parameters.TryGetValue("group-count", out int? groupCount) && groupCount.HasValue))
                throw new ArgumentException("Chart expected parameter \"group-count\", which was not supplied");
            if (!(parameters.TryGetValue("xdat", out DataFrame? xdatFrame) && xdatFrame is not null))
                throw new ArgumentException("Chart expected parameter \"xdat\", which was not supplied");
            if (!(parameters.TryGetValue("cdat", out DataFrame? cdatFrame) && cdatFrame is not null))
                throw new ArgumentException("Chart expected parameter \"cdat\", which was not supplied");
            if (!(parameters.TryGetValue("ydat", out DataFrame? ydatFrame) && ydatFrame is not null))
                throw new ArgumentException("Chart expected parameter \"ydat\", which was not supplied");
            parameters.TryGetValue("ydatl", out DataFrame? ydatlFrame);
            parameters.TryGetValue("ydatu", out DataFrame? ydatuFrame);

            List<SurvivalOptions.SurvivalSeries> series = new();
            for (int i = 0; i < groupCount.Value; i++)
            {
                SurvivalOptions.SurvivalSeries survivalSeries = new();
                // Series: xdat...
                DoubleVariable xdatVariable = (DoubleVariable)xdatFrame.Variables[i];
                survivalSeries.XDat = xdatVariable.Data;
                // ... cdat...
                DoubleVariable cdatVariable = (DoubleVariable)cdatFrame.Variables[i];
                int[] cdat = new int[cdatVariable.Length];
                for (int r = 0; r < cdat.Length; r++)
                    cdat[r] = cdatVariable.Data[r] == Constant.MISSING
                        ? -1
                        : (int)cdatVariable.Data[r];
                survivalSeries.CDat = cdat;
                // ... ydat...
                DoubleVariable ydatVariable = (DoubleVariable)ydatFrame.Variables[i];
                survivalSeries.YDat = ydatVariable.Data;
                // ... ydatl...
                if (ydatlFrame is not null)
                {
                    DoubleVariable ydatlVariable = (DoubleVariable)ydatlFrame.Variables[i];
                    survivalSeries.YDatL = ydatlVariable.Data;
                }
                // ... and ydatu
                if (ydatuFrame is not null)
                {
                    DoubleVariable ydatuVariable = (DoubleVariable)ydatuFrame.Variables[i];
                    survivalSeries.YDatU = ydatuVariable.Data;
                }
                series.Add(survivalSeries);
            }
            string[] seriesTitles = new string[groupCount.Value];
            for (int i = 0; i < groupCount.Value; i++)
                seriesTitles[i] = $"{i + 1}";
            SurvivalOptions survivalOptions = new(ChartPreferences,
                series: series,
                seriesTitles: seriesTitles,
                shouldAutoscale: !ChartPreferences.RequestScaleLimits,
                showEventMarkers: true,
                showLegend: groupCount.Value > 1,
                title: dataName is null
                    ? "Survival plot"
                    : $"Survival plot from {dataName}",
                yAxisTitle: "Survival proportion"
            );
            survivalOptions.SetMarkers();
            return survivalOptions;
        }

        public void PostProcessFilledChartOptions(ChartDefinition definition)
        {
            switch (definition.ChartType)
            {
                case ChartType.Bar:
                case ChartType.StackedBar:
                case ChartType.StackedBar100Percent:
                    {
                        BarOptions barOptions = (BarOptions)definition.ChartOptions;
                        StringSeries labelsSeries = new(barOptions.SeriesTitles.Count);
                        for (int i = 0; i < barOptions.SeriesTitles.Count; i++)
                            labelsSeries.Data[i] = barOptions.SeriesTitles[i];
                        definition.XSeries.Clear();
                        definition.XSeries.Add(labelsSeries);
                        break;
                    }
                case ChartType.BoxWhisker:
                    {
                        BoxWhiskerOptions bwOptions = (BoxWhiskerOptions)definition.ChartOptions;
                        IReadOnlyList<ISeries> seriesToUse = definition.YSeries.Count > 0
                            ? definition.YSeries
                            : definition.XSeries;
                        for (int i = 0; i < seriesToUse.Count; i++)
                            seriesToUse[i].Title = bwOptions.SeriesTitles[i];
                        // Reverse series before plotting if required
                        if (bwOptions.Orientation == ChartOrientation.Horizontal && definition.XSeries.Count > 0
                            || bwOptions.Orientation == ChartOrientation.Vertical && definition.YSeries.Count > 0)
                            definition.SwapXAndYSeries();
                        break;
                    }
                case ChartType.Ladder:
                    {
                        LadderOptions ladderOptions = (LadderOptions)definition.ChartOptions;
                        for (int i = 0; i < definition.YSeries.Count; i++)
                            definition.YSeries[i].Title = ladderOptions.SeriesTitles[i];
                        break;
                    }
                case ChartType.Spread:
                    {
                        SpreadOptions spreadOptions = (SpreadOptions)definition.ChartOptions;
                        IReadOnlyList<ISeries> seriesToUse = definition.YSeries.Count > 0
                            ? definition.YSeries
                            : definition.XSeries;
                        for (int i = 0; i < seriesToUse.Count; i++)
                            seriesToUse[i].Title = spreadOptions.SeriesTitles[i];
                        break;
                    }
                case ChartType.LineXY:
                case ChartType.ScatterXY:
                    {
                        ScatterXYOptions options = (ScatterXYOptions)definition.ChartOptions;
                        IReadOnlyList<ISeries> seriesToUse = definition.XSeries;
                        for (int i = 0; i < seriesToUse.Count; i++)
                            seriesToUse[i].Title = options.SeriesTitles[i];
                        break;
                    }
                case ChartType.AgreementPair:
                case ChartType.BiasMA:
                case ChartType.Control:
                case ChartType.ErrorBar:
                case ChartType.Forest:
                case ChartType.Gini:
                case ChartType.Histogram:
                case ChartType.LinearRegression:
                case ChartType.Normal:
                case ChartType.Pyramid:
                case ChartType.ROC:
                case ChartType.Survival:
                    {
                        // Do nothing
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException(nameof(definition), definition, "definition.ChartType: Not all types can be plotted yet");
            }
        }

        public static DoubleSeries VariableToSeries(DoubleVariable variable) => new(variable.Data, variable.Title);
        public static StringSeries VariableToSeries(StringVariable variable) => new(variable.Data, variable.Title);
    }
}
