// #define SECURE

using System;
using System.Collections.Generic;
using System.Diagnostics;
using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.UI;
using StatsDirect.Utilities;

namespace StatsDirect.Templates
{
    /// <summary>
    /// An interface-agnostic template operation processor.
    /// </summary>
    public sealed class TemplateProcessor : ITemplateProcessor
    {
        private readonly ITemplateHost host;
        private const string STATSDIRECT_CHART_OPTIONS = "statsdirect-chart-options";
        private const string STATSDIRECT_FRAME_PANE = "statsdirect-frame-pane";
        private const string STATSDIRECT_REPORT_PANE = "statsdirect-report-pane";

        public TemplateProcessor(ITemplateHost host)
        {
            this.host = host;
        }

        /// <summary>
        /// Run the operation to completion or error.
        /// </summary>
        /// <param name="Operation"></param>
        /// <param name="startingParameters">If non-null, some parameters to be used as defaults.</param>
        /// <param name="isRedo"> </param>
        public ParameterBag Execute(Operation Operation, ParameterBag startingParameters, bool isRedo)
        {
            host.Operation = Operation;
            ParameterBag filledParameters = startingParameters ?? new ParameterBag();

            // Prepare the steps, to give an opportunity for some parts of the system to set themselves up
            foreach (Step step in Operation.Steps)
            {
                Prepare(step, filledParameters);
            }

            // Run each step in turn
            foreach (Step step in Operation.Steps)
            {
                try
                {
                    StepResult result = Execute(step, filledParameters, isRedo);
                    filledParameters = (null == result) ? null : result.ParameterBag;
                }
                catch (InvalidDataException ex)
                {
                    ex.InputParameters = filledParameters;
                    throw;
                }
                catch (TemplateOperationCancelledException)
                {
                    // The user cancelled the operation
                    return null;
                }
            }
            host.Operation = null;
            return filledParameters;
        }

        /// <summary>
        /// Prepare the operation with the passed-in parameters.
        /// </summary>
        /// <param name="step"></param>
        /// <param name="parameters"></param>
        private void Prepare(Step step, ParameterBag parameters)
        {
            step.PrepareInternal(this, parameters);
        }

        public void PrepareInternal(ParametersStep step, ParameterBag parameters)
        {
            foreach (Parameter parameter in step.Parameters)
            {
                host.PrepareParameter(this, parameter, parameters);
            }
        }

        /// <summary>
        /// Execute the operation with the passed-in parameters, returning some results that can be used for the next operation.
        /// Implementers <strong>must</strong> ensure that a new dictionary is used for the output.
        /// </summary>
        /// <param name="step"></param>
        /// <param name="parameters"></param>
        /// <param name="isRedo"></param>
        /// <returns></returns>
        private StepResult Execute(Step step, ParameterBag parameters, bool isRedo)
        {
            StepResult result = null;
            if (null != parameters)
                result = step.ExecuteInternal(this, parameters, isRedo);

            // If required, transfer input parameters where the same name is not already present in the results.
            if (null != result)
            {
                ParameterBag results = result.ParameterBag;
                if (null != results)
                {
                    if (step.ShouldCopyInputParameters)
                    {
                        foreach (KeyValuePair<string, FilledParameter> inputParameter in parameters.Pairs)
                            if (!results.ContainsKey(inputParameter.Key))
                                results.Add(inputParameter.Key, inputParameter.Value);
                    }
                    // Remove explicit blanks now that they have prevented copying.
                    List<string> keysToRemove = new List<string>();
                    foreach (KeyValuePair<string, FilledParameter> pair in results.Pairs)
                        if (null == pair.Value)
                            keysToRemove.Add(pair.Key);
                    foreach (string keyToRemove in keysToRemove)
                        results.Remove(keyToRemove);
                }
            }
            return result;
        }

        public StepResult ExecuteInternal(BuiltinStep step, ParameterBag parameters, bool isRedo)
        {
            if (null == parameters)
                throw new ArgumentOutOfRangeException("parameters", "parameters must be a dictionary and cannot be null. Did a previous script step return null?");
            Builtin builtin = BuiltinRegistry.SoleInstance.Builtin(step.FunctionName);
            if (null == builtin)
                throw new Exception("No function '" + step.FunctionName + "' is supplied by the host.");
            BuiltinFunction toCall = builtin.FunctionToCall;
            StepResult outputResult = toCall(host, parameters);
            // Ensure no stray progress bars stay around
            host.FinishProgress();
            return outputResult;
        }

        ChartOptions FillChartOptions(ChartStep step, ParameterBag parameters, bool isRedo, ChartDefinition definition, string dataName)
        {
            ChartOptions options = FindOrPreprocessChartOptions(step, parameters, isRedo, definition, dataName);
            definition.ChartOptions = options;
            if (step.RequestUserInput)
            {
                if (null == host.Amend(definition, parameters))
                    throw new TemplateOperationCancelledException();
            }
            PostProcessFilledChartOptions(step, definition);
            return options;
        }

        private ChartOptions FindOrPreprocessChartOptions(ChartStep step, ParameterBag parameters, bool isRedo, ChartDefinition definition, string dataName)
        {
            // If we're redoing a previous operation, we should in theory have the previous ChartOptions.  Go look!
            if (isRedo)
            {
                string possibleParameterName = STATSDIRECT_CHART_OPTIONS + (step.ChartName ?? "");
                FilledParameter fp;
                if (parameters.TryGetValue(possibleParameterName, out fp))
                {
                    if (null != fp && fp.HasData)
                        return (ChartOptions)fp.AsChartOptions;
                }
            }

            // If we're not redoing, or we can't find the options, then we need to fill them in now.
            // Chart options
            ChartOptions options;
            switch (step.ChartType)
            {
                case ChartType.AgreementPair:
                    {
                        AgreementOptions aOptions = new AgreementOptions(host.Preferences.ShouldUseColour)
                                                        {
                                                            ShouldAutoscale = !ChartRenderer.DefaultRequestScaleLimits,
                                                            Title = step.ChartTitle,
                                                            lla = parameters["lla"].AsDouble,
                                                            mean = parameters["mean"].AsDouble,
                                                            ula = parameters["ula"].AsDouble,
                                                            P0 = parameters["P0"].AsDouble,
                                                            av =
                                                                parameters["av"].AsDataFrame.Variables[0].
                                                                AsDoubleVariable.Data,
                                                            mxd =
                                                                parameters["mxd"].AsDataFrame.Variables[0].
                                                                AsDoubleVariable.Data
                                                        };
                        options = aOptions;
                        break;
                    }
                case ChartType.Bar:
                case ChartType.StackedBar:
                case ChartType.StackedBar100Percent:
                    {
                        BarOptions barOptions = new BarOptions(host.Preferences.ShouldUseColour)
                                                    {
                                                        ShouldAutoscale = !ChartRenderer.DefaultRequestScaleLimits,
                                                        Title =
                                                            null == dataName
                                                                ? "Bar chart"
                                                                : "Bar chart from " + dataName
                                                    };

                        if (ChartType.StackedBar == step.ChartType || ChartType.StackedBar100Percent == step.ChartType)
                        {
                            barOptions.Stacked = true;
                            barOptions.Stacked100Percent = ChartType.StackedBar100Percent == step.ChartType;
                            barOptions.MaxBarWidth = 0.6; // Default 60% bar width
                        }
                        else
                        {
                            barOptions.MaxBarWidth = 0.6 / definition.YSeries.Count;
                        }
                        DataFrame labelsFrame = parameters["labels"].AsDataFrame;
                        StringVariable labelsVariable = labelsFrame.Variables[0].AsStringVariable;
                        barOptions.SeriesTitles = new string[labelsVariable.Length];
                        for (int i = 0; i < labelsVariable.Length; i++)
                            barOptions.SeriesTitles[i] = labelsVariable.Data[i];
                        barOptions.SetMarkers(definition.YSeries);
                        barOptions.Orientation = ChartOrientation.Vertical;
                        barOptions.ShowLegend = 1 < definition.YSeries.Count;
                        if (!barOptions.ShowLegend)
                            barOptions.YAxisTitle = definition.YSeries[0].Title;
                        options = barOptions;
                        break;
                    }
                case ChartType.BoxWhisker:
                    {
                        BoxWhiskerOptions bwOptions = new BoxWhiskerOptions(host.Preferences.ShouldUseColour)
                                                          {
                                                              Title = null == dataName
                                                                          ? "Box & whisker plot"
                                                                          : "Box & whisker plot from " + dataName
                                                          };

                        // If only X series have been passed in, we're vertical.  If only Y, we're horizontal.  If both or neither, we can't plot.
                        if (0 == definition.XSeries.Count && 0 == definition.YSeries.Count)
                            throw new ArgumentException("Must have at least one series to plot a box+whisker plot");
                        if (definition.XSeries.Count > 0 && definition.YSeries.Count > 0)
                            throw new ArgumentException("Cannot plot a box+whisker plot with both X and Y series");
                        IList<Series> seriesToUse = definition.YSeries.Count > 0 ? definition.YSeries : definition.XSeries;
                        bwOptions.Orientation = (definition.YSeries.Count > 0) ? ChartOrientation.Horizontal : ChartOrientation.Vertical;

                        bwOptions.SeriesTitles = new string[seriesToUse.Count];
                        for (int i = 0; i < seriesToUse.Count; i++)
                            bwOptions.SeriesTitles[i] = seriesToUse[i].Title;
                        bwOptions.IsAscii = step.IsAscii;
                        bwOptions.MarkMeanAndMedian = false;
                        bwOptions.UseInnerFence = true;
                        bwOptions.UseOuterFence = true;
                        options = bwOptions;
                        break;
                    }
                case ChartType.Control:
                    {
                        ControlOptions controlOptions = new ControlOptions(host.Preferences.ShouldUseColour)
                                                            {
                                                                ShouldBoxAxes = ChartRenderer.DefaultBoxAxes,
                                                                ShouldAutoscale =
                                                                    !ChartRenderer.DefaultRequestScaleLimits,
                                                                RightHandDecimalPlaces = 3,
                                                                Title =
                                                                    null == dataName
                                                                        ? "Control chart"
                                                                        : "Control chart from " + dataName
                                                            };
                        // We need exactly one of each series
                        if (1 != definition.YSeries.Count || 1 != definition.XSeries.Count)
                            throw new ArgumentException("Must have exactly one X series and one Y series for a control chart");

                        // This is a duplicate of the top analysis in PlotControl.
                        DoubleSeries xs0 = definition.XSeries[0] as DoubleSeries;
                        DoubleSeries ys0 = definition.YSeries[0] as DoubleSeries;
                        Debug.Assert(null != xs0 && null != ys0);
                        int rows = xs0.Points;
                        double[] xdat = new double[rows];
                        double[] ydat = new double[rows];

                        int ctr = 0;
                        bool looksLikeDates = true;
                        double[] ySeriesData = ys0.Data;
                        double[] xSeriesData = xs0.Data;
                        for (int r = 0; r < rows; r++)
                        {
                            if (ySeriesData[r] != Constant.MISSING && xSeriesData[r] != Constant.MISSING)
                            {
                                xdat[ctr] = xSeriesData[r];
                                ydat[ctr] = ySeriesData[r];
                                if (xdat[ctr] < 20000)
                                    looksLikeDates = false;
                                ctr++;
                            }
                        }
                        rows = ctr;

                        double ymean;
                        double ysd;
                        MathDbl.meansd(ydat, 0, ref rows, out ymean, out ysd);

                        controlOptions.UseDates = looksLikeDates;
                        controlOptions.ObservationsToUse = rows;

                        controlOptions.YAxisTitle = definition.YSeries[0].Title;
                        controlOptions.XAxisTitle = definition.XSeries[0].Title;
                        controlOptions.UseMean = true;
                        controlOptions.Use1SD = true;
                        controlOptions.Use2SD = true;
                        controlOptions.Use3SD = true;
                        options = controlOptions;
                        break;
                    }
                case ChartType.ErrorBar:
                    {
                        if (!parameters.ContainsKey("xdat"))
                            throw new Exception("Chart expected parameter \"xdat\", which was not supplied");
                        if (!parameters.ContainsKey("ydat"))
                            throw new Exception("Chart expected parameter \"ydat\", which was not supplied");
                        if (!parameters.ContainsKey("ydatl"))
                            throw new Exception("Chart expected parameter \"ydatl\", which was not supplied");
                        if (!parameters.ContainsKey("ydatu"))
                            throw new Exception("Chart expected parameter \"ydatu\", which was not supplied");

                        ErrorBarOptions errorBarOptions = new ErrorBarOptions(host.Preferences.ShouldUseColour)
                                                              {
                                                                  ShouldAutoscale =
                                                                      !ChartRenderer.DefaultRequestScaleLimits,
                                                                  Title = null == dataName
                                                                              ? "Error bar plot"
                                                                              : "Error bar plot plot from " + dataName,
                                                                  xdat = parameters["xdat"].AsDataFrame,
                                                                  ydat = parameters["ydat"].AsDataFrame,
                                                                  ydatl = parameters["ydatl"].AsDataFrame,
                                                                  ydatu = parameters["ydatu"].AsDataFrame
                                                              };
                        errorBarOptions.SeriesTitles = new string[errorBarOptions.ydat.VariableCount];
                        errorBarOptions.YAxisTitle = errorBarOptions.ydat.Variables[0].Title;
                        errorBarOptions.XAxisTitle = errorBarOptions.xdat.Variables[0].Title;
                        for (int i = 0; i < errorBarOptions.ydat.VariableCount; i++)
                        {
                            errorBarOptions.SeriesTitles[i] =
                                string.IsNullOrEmpty(errorBarOptions.ydat.Variables[i].Title)
                                    ? "Series " + (i + 1).ToString()
                                    : errorBarOptions.ydat.Variables[i].Title;
                        }
                        errorBarOptions.SetMarkers();
                        options = errorBarOptions;
                        break;
                    }
                case ChartType.Forest:
                    {
                        ForestOptions fOptions = new ForestOptions(host.Preferences.ShouldUseColour)
                                                     {
                                                         ShouldAutoscale = !ChartRenderer.DefaultRequestScaleLimits,
                                                         Title =
                                                             null == dataName
                                                                 ? "Forest plot"
                                                                 : "Forest plot from " + dataName,
                                                         k = parameters["odds"].AsDataFrame.Variables[0].Length,
                                                         odr =
                                                             parameters["odds"].AsDataFrame.Variables[0].
                                                             AsDoubleVariable.Data,
                                                         odrl =
                                                             parameters["lci"].AsDataFrame.Variables[0].AsDoubleVariable
                                                             .Data,
                                                         odru =
                                                             parameters["uci"].AsDataFrame.Variables[0].AsDoubleVariable
                                                             .Data
                                                     };
                        if (parameters.ContainsKey("gn") && null != parameters["gn"])
                        {
                            fOptions.gn = parameters["gn"].AsDataFrame.Variables[0].AsDoubleVariable.Data;
                        }
                        else
                        {
                            double[] gn = new double[fOptions.k];
                            for (int i = 0; i < fOptions.k; i++)
                            {
                                gn[i] = 10;
                            }
                            fOptions.gn = gn;
                        }
                        if (parameters.ContainsKey("pg") && null != parameters["pg"])
                            fOptions.pg = parameters["pg"].AsDataFrame.Variables[0].AsDoubleVariable.Data;
                        fOptions.XAxisTitle = "odds ratio (95% confidence interval)";
                        if (parameters.ContainsKey("title") && null != parameters["title"])
                        {
                            fOptions.titles = parameters["title"].AsDataFrame.Variables[0].AsStringVariable.Data;
                        }
                        else
                        {
                            string[] titles = new string[fOptions.k];
                            for (int i = 0; i < fOptions.k; i++)
                            {
                                titles[i] = "stratum " + (i + 1).ToString();
                            }
                            fOptions.titles = titles;
                        }

                        // Sort out candidate decimal places
                        double absmin = Double.MaxValue;
                        for (int i = 0; i < fOptions.k; i++)
                        {
                            if (Math.Abs(fOptions.odr[i]) < absmin && fOptions.odr[i] != 0.0)
                                absmin = Math.Abs(fOptions.odr[i]);
                            if (Math.Abs(fOptions.odrl[i]) < absmin && fOptions.odrl[i] != 0.0)
                                absmin = Math.Abs(fOptions.odrl[i]);
                            if (Math.Abs(fOptions.odru[i]) < absmin && fOptions.odru[i] != 0.0)
                                absmin = Math.Abs(fOptions.odru[i]);
                        }
                        int decpm = 2;
                        try
                        {
                            if (absmin < Math.Pow(10D, -decpm) && absmin != 0D)
                            {
                                decpm = 3;
                                if (absmin < Math.Pow(10D, -decpm) && absmin != 0D)
                                    decpm = 4;
                            }
                        }
                        catch
                        {
                            // Do nothing; keep decpm=2
                        }
                        fOptions.EffectSizeAndIntervalDecimalPlaces = decpm;

                        options = fOptions;
                        break;
                    }
                case ChartType.Gini:
                    {
                        GiniOptions giniOptions = new GiniOptions(host.Preferences.ShouldUseColour)
                                                      {
                                                          ShouldAutoscale = !ChartRenderer.DefaultRequestScaleLimits,
                                                          Title =
                                                              null == dataName
                                                                  ? "Lorenz plot"
                                                                  : "Lorenz plot for " + dataName
                                                      };

                        // We need exactly one of each series
                        if (1 != definition.YSeries.Count || 1 != definition.XSeries.Count)
                            throw new ArgumentException("Must have exactly one X series and one Y series for a Gini chart");
                        options = giniOptions;
                        break;
                    }
                case ChartType.Histogram:
                    {
                        List<Series> series = definition.XSeries.Count > 0 ? definition.XSeries : definition.YSeries;
                        HistogramOptions hOptions = new HistogramOptions(host.Preferences.ShouldUseColour)
                                                    {
                                                        IsAscii = step.IsAscii,
                                                        LineWidth = 2,
                                                        HistoSeriesOptions = new List<HistogramSeriesOptions>(series.Count)
                                                    };

                        // Series
                        foreach (Series t in series)
                        {
                            HistogramSeriesOptions hso = new HistogramSeriesOptions
                                                             {
                                                                 ChartTitle =
                                                                     "Distribution of " + t.Title,
                                                                 YAxisTitle = "Counts",
                                                                 XAxisTitle =
                                                                     "Mid-points for " + t.Title
                                                             };
                            hOptions.HistoSeriesOptions.Add(hso);
                        }
                        options = hOptions;
                        break;
                    }
                case ChartType.Ladder:
                    {
                        // We need exactly 2 Y series
                        if (2 != definition.YSeries.Count || 0 != definition.XSeries.Count)
                            throw new ArgumentException("Must have exactly two Y series for a ladder plot");

                        LadderOptions ladderOptions = new LadderOptions(host.Preferences.ShouldUseColour)
                                                          {
                                                              ShouldAutoscale = !ChartRenderer.DefaultRequestScaleLimits,
                                                              Title =
                                                                  null == dataName
                                                                      ? "Ladder plot"
                                                                      : "Ladder plot from " + dataName,
                                                              SeriesTitles = new string[definition.YSeries.Count]
                                                          };

                        for (int i = 0; i < definition.YSeries.Count; i++)
                            ladderOptions.SeriesTitles[i] = definition.YSeries[i].Title;
                        options = ladderOptions;
                        break;
                    }
                case ChartType.LineXY:
                    {
                        ScatterXYOptions sOptions = new ScatterXYOptions(host.Preferences.ShouldUseColour,
                                                                         definition.XSeries, true)
                                                        {
                                                            ShouldAutoscale = !ChartRenderer.DefaultRequestScaleLimits,
                                                            Title =
                                                                null == dataName
                                                                    ? "Line plot"
                                                                    : "Line plot from " + dataName,
                                                            YAxisTitle = definition.YSeries[0].Title,
                                                            XAxisTitle = definition.XSeries[0].Title,
                                                            PlotMarkers = true
                                                        };
                        options = sOptions;
                        break;
                    }
                case ChartType.LinearRegression:
                    {
                        LinearRegressionOptions lrOptions = new LinearRegressionOptions(host.Preferences.ShouldUseColour)
                                                                {
                                                                    Slope = parameters["mdnValue"].AsDouble,
                                                                    Intercept = parameters["interceptValue"].AsDouble,
                                                                    FullWidth =
                                                                        (parameters.ContainsKey("chartIsFullWidth") &&
                                                                         parameters["chartIsFullWidth"].AsBoolean),
                                                                    Title = step.ChartTitle,
                                                                    XAxisTitle = parameters["xtitle"].AsString,
                                                                    YAxisTitle = parameters["ytitle"].AsString
                                                                };
                        options = lrOptions;
                        break;
                    }
                case ChartType.Normal:
                    {
                        NormalOptions nOptions = new NormalOptions(host.Preferences.ShouldUseColour)
                                                     {
                                                         ShouldAutoscale = !ChartRenderer.DefaultRequestScaleLimits,
                                                         Method = parameters.ContainsKey("ScoreMethod")
                                                                      ? (NormalOptions.ScoreMethod)
                                                                        Parsing.Cint_Txt(parameters["ScoreMethod"].AsString)
                                                                      : NormalOptions.ScoreMethod.VanDerWaerden,
                                                         Title =
                                                             null == dataName
                                                                 ? "Normal plot"
                                                                 : "Normal plot from " + dataName
                                                     };
                        options = nOptions;
                        break;
                    }
                case ChartType.Pyramid:
                    {
                        PyramidOptions pOptions = new PyramidOptions(host.Preferences.ShouldUseColour) { ShouldAutoscale = !ChartRenderer.DefaultRequestScaleLimits };
                        if (parameters.ContainsKey("Male"))
                        {
                            pOptions.MaleFrame = parameters["Male"].AsDataFrame;
                        }
                        if (parameters.ContainsKey("Female"))
                        {
                            pOptions.FemaleFrame = parameters["Female"].AsDataFrame;
                        }
                        if (parameters.ContainsKey("Labels"))
                        {
                            pOptions.LabelFrame = parameters["Labels"].AsDataFrame;
                        }
                        /**
                        if (parameters.ContainsKey("Shading"))
                        {
                            pOptions.Shading = (SDChart.FillStyle)Parsing.Cint_Txt(parameters["Shading"].AsString);
                        }
                         */
                        pOptions.Title = null == dataName ? "Population pyramid" : "Population pyramid from " + dataName;
                        pOptions.SetOptions();
                        options = pOptions;
                        break;
                    }
                case ChartType.ScatterXY:
                    {
                        ScatterXYOptions sOptions = new ScatterXYOptions(host.Preferences.ShouldUseColour,
                                                                         definition.XSeries, false)
                                                        {
                                                            IsAscii = step.IsAscii,
                                                            ShouldAutoscale = !ChartRenderer.DefaultRequestScaleLimits,
                                                            Title =
                                                                null == dataName
                                                                    ? "Scatter plot"
                                                                    : "Scatter plot from " + dataName,
                                                            YAxisTitle = definition.YSeries[0].Title,
                                                            XAxisTitle = definition.XSeries[0].Title
                                                        };
                        options = sOptions;
                        break;
                    }
                case ChartType.ROC:
                    {
                        if (!parameters.ContainsKey("series-count"))
                            throw new Exception("Chart expected parameter \"series-count\", which was not supplied");
                        int seriesCount = parameters["series-count"].AsInt32;
                        for (int i = 1; i <= seriesCount; i++)
                        {
                            // Series: First present...
                            string presentDataName = "P" + i.ToString();
                            if (!parameters.ContainsKey(presentDataName))
                                throw new Exception("Chart expected parameter \"" + presentDataName + "\", which was not supplied");
                            DataFrame frame = parameters[presentDataName].AsDataFrame;
                            for (int v = 0; v < frame.VariableCount; v++)
                            {
                                DoubleVariable variable = frame.Variables[v].AsDoubleVariable;
                                definition.AddXSeriesAt(VariableToSeries(variable), v);
                            }
                            // dataName = frame.Name;
                            // ... then absent
                            string absentDataName = "A" + i.ToString();
                            if (!parameters.ContainsKey(absentDataName))
                                throw new Exception("Chart expected parameter \"" + absentDataName + "\", which was not supplied");
                            frame = parameters[absentDataName].AsDataFrame;
                            for (int v = 0; v < frame.VariableCount; v++)
                            {
                                DoubleVariable variable = frame.Variables[v].AsDoubleVariable;
                                definition.AddYSeriesAt(VariableToSeries(variable), v);
                            }
                            dataName = frame.Name;
                        }
                        double pmn = 1;
                        double amn = 1;
                        for (int C = 0; C < definition.XSeries.Count; C++)
                        {
                            DoubleSeries xs = definition.XSeries[C].AsDoubleSeries;
                            DoubleSeries ys = definition.YSeries[C].AsDoubleSeries;
                            pmn = xs.Sum / xs.Points;
                            amn = ys.Sum / ys.Points;
                        }

                        ROCOptions rocOptions = new ROCOptions(host.Preferences.ShouldUseColour, definition.XSeries)
                                                {
                                                    ShouldAutoscale = !ChartRenderer.DefaultRequestScaleLimits,
                                                    Title =
                                                        null == dataName ? "ROC plot" : "ROC plot from " + dataName,
                                                    ShowCutOffCalculator = true,
                                                    ShowOptimumCutOff = true,
                                                    Weight = 1.0,
                                                    GAMMA = host.Preferences.DefaultConfidenceInterval, Showopts = (pmn > amn) ? ComparisonValue.GE : ComparisonValue.LE
                                                };

                        if (parameters.ContainsKey("GAMMA"))
                            rocOptions.GAMMA = parameters["GAMMA"].AsDouble;

                        rocOptions.SeriesTitles = new string[seriesCount];
                        for (int i = 0; i < seriesCount; i++)
                            rocOptions.SeriesTitles[i] = definition.XSeries[i].Title + " (+ve), " + definition.YSeries[i].Title + " (-ve)";
                        options = rocOptions;
                        break;
                    }
                case ChartType.Spread:
                    {
                        // If only X series have been passed in, we're vertical.  If only Y, we're horizontal.  If both or neither, we can't plot.
                        if (0 == definition.XSeries.Count && 0 == definition.YSeries.Count)
                            throw new ArgumentException("Must have at least one series to plot a spread plot");
                        if (definition.XSeries.Count > 0 && definition.YSeries.Count > 0)
                            throw new ArgumentException("Cannot plot a spread plot with both X and Y series");
                        IList<Series> seriesToUse = definition.YSeries.Count > 0 ? definition.YSeries : definition.XSeries;

                        SpreadOptions spreadOptions = new SpreadOptions(host.Preferences.ShouldUseColour)
                                                          {
                                                              ShouldAutoscale = !ChartRenderer.DefaultRequestScaleLimits,
                                                              Title =
                                                                  null == dataName
                                                                      ? "Spread plot"
                                                                      : "Spread plot from " + dataName,
                                                              SeriesTitles = new string[seriesToUse.Count]
                                                          };
                        for (int i = 0; i < seriesToUse.Count; i++)
                            spreadOptions.SeriesTitles[i] = seriesToUse[i].Title;
                        options = spreadOptions;
                        break;
                    }
                case ChartType.Survival:
                    {
                        SurvivalOptions survivalOptions = new SurvivalOptions(host.Preferences.ShouldUseColour)
                                                              {
                                                                  ShouldAutoscale =
                                                                      !ChartRenderer.DefaultRequestScaleLimits,
                                                                  Title =
                                                                      null == dataName
                                                                          ? "Survival plot"
                                                                          : "Survival plot from " + dataName
                                                              };

                        if (!parameters.ContainsKey("group-count"))
                            throw new Exception("Chart expected parameter \"group-count\", which was not supplied");
                        int groupCount = parameters["group-count"].AsInt32;

                        if (!parameters.ContainsKey("xdat"))
                            throw new Exception("Chart expected parameter \"xdat\", which was not supplied");
                        DataFrame xdatFrame = parameters["xdat"].AsDataFrame;

                        if (!parameters.ContainsKey("cdat"))
                            throw new Exception("Chart expected parameter \"cdat\", which was not supplied");
                        DataFrame cdatFrame = parameters["cdat"].AsDataFrame;

                        if (!parameters.ContainsKey("ydat"))
                            throw new Exception("Chart expected parameter \"ydat\", which was not supplied");
                        DataFrame ydatFrame = parameters["ydat"].AsDataFrame;

                        DataFrame ydatlFrame = null;
                        if (parameters.ContainsKey("ydatl") && null != parameters["ydatl"])
                        {
                            ydatlFrame = parameters["ydatl"].AsDataFrame;
                        }

                        DataFrame ydatuFrame = null;
                        if (parameters.ContainsKey("ydatu") && null != parameters["ydatu"])
                        {
                            ydatuFrame = parameters["ydatu"].AsDataFrame;
                        }

                        for (int i = 0; i < groupCount; i++)
                        {
                            SurvivalOptions.SurvivalSeries ser = new SurvivalOptions.SurvivalSeries();
                            // Series: xdat...
                            DoubleVariable xdatVariable = xdatFrame.Variables[i].AsDoubleVariable;
                            ser.XDat = xdatVariable.Data;
                            // ... cdat...
                            DoubleVariable cdatVariable = cdatFrame.Variables[i].AsDoubleVariable;
                            int[] cdat = new int[cdatVariable.Length];
                            for (int r = 0; r < cdat.Length; r++)
                            {
                                if (cdatVariable.Data[r] == Constant.MISSING)
                                    cdat[r] = -1;
                                else
                                    cdat[r] = (int)cdatVariable.Data[r];
                            }
                            ser.CDat = cdat;
                            // ... ydat...
                            DoubleVariable ydatVariable = ydatFrame.Variables[i].AsDoubleVariable;
                            ser.YDat = ydatVariable.Data;
                            // ... ydatl...
                            if (null != ydatlFrame)
                            {
                                DoubleVariable ydatlVariable = ydatlFrame.Variables[i].AsDoubleVariable;
                                ser.YDatL = ydatlVariable.Data;
                            }
                            // ... and ydatu
                            if (null != ydatuFrame)
                            {
                                DoubleVariable ydatuVariable = ydatuFrame.Variables[i].AsDoubleVariable;
                                ser.YDatU = ydatuVariable.Data;
                            }
                            survivalOptions.Series.Add(ser);
                        }
                        survivalOptions.SeriesTitles = new string[groupCount];
                        survivalOptions.ShowEventMarkers = true;
                        survivalOptions.YAxisTitle = "Survival proportion";
                        for (int i = 0; i < groupCount; i++)
                            survivalOptions.SeriesTitles[i] = "Series " + (i + 1).ToString();
                        survivalOptions.SetMarkers();
                        options = survivalOptions;
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException("step", step, "step.ChartType: Not all types can be plotted yet");
            }
            return options;
        }

        private static void PostProcessFilledChartOptions(ChartStep step, ChartDefinition definition)
        {
            switch (step.ChartType)
            {
                case ChartType.Bar:
                case ChartType.StackedBar:
                case ChartType.StackedBar100Percent:
                    {
                        BarOptions barOptions = (BarOptions)definition.ChartOptions;
                        StringSeries labelsSeries = new StringSeries(barOptions.SeriesTitles.Length);
                        for (int i = 0; i < barOptions.SeriesTitles.Length; i++)
                            labelsSeries.Data[i] = barOptions.SeriesTitles[i];
                        definition.XSeries.Add(labelsSeries);
                        break;
                    }
                case ChartType.BoxWhisker:
                    {
                        BoxWhiskerOptions bwOptions = (BoxWhiskerOptions)definition.ChartOptions;
                        IList<Series> seriesToUse = definition.YSeries.Count > 0 ? definition.YSeries : definition.XSeries;
                        for (int i = 0; i < seriesToUse.Count; i++)
                            seriesToUse[i].Title = bwOptions.SeriesTitles[i];
                        // Reverse series before plotting if required
                        if ((bwOptions.Orientation == ChartOrientation.Horizontal && definition.XSeries.Count > 0) || (bwOptions.Orientation == ChartOrientation.Vertical && definition.YSeries.Count > 0))
                        {
                            List<Series> temp = definition.YSeries;
                            definition.YSeries = definition.XSeries;
                            definition.XSeries = temp;
                        }
                        break;
                    }
                case ChartType.Ladder:
                    {
                        LadderOptions ladderOptions = (LadderOptions)definition.ChartOptions;
                        for (int i = 0; i < definition.YSeries.Count; i++)
                            definition.YSeries[i].Title = ladderOptions.SeriesTitles[i];
                        break;
                    }
                case ChartType.LineXY:
                    {
                        break;
                    }
                case ChartType.Spread:
                    {
                        SpreadOptions spreadOptions = (SpreadOptions)definition.ChartOptions;
                        IList<Series> seriesToUse = definition.YSeries.Count > 0 ? definition.YSeries : definition.XSeries;
                        for (int i = 0; i < seriesToUse.Count; i++)
                            seriesToUse[i].Title = spreadOptions.SeriesTitles[i];
                        break;
                    }
                case ChartType.AgreementPair:
                case ChartType.Control:
                case ChartType.ErrorBar:
                case ChartType.Forest:
                case ChartType.Gini:
                case ChartType.Histogram:
                case ChartType.LinearRegression:
                case ChartType.Normal:
                case ChartType.Pyramid:
                case ChartType.ROC:
                case ChartType.ScatterXY:
                case ChartType.Survival:
                    {
                        // Do nothing
                        break;
                    }
                default:
                    throw new ArgumentOutOfRangeException("step", step, "step.ChartType: Not all types can be plotted yet");
            }
        }

        public StepResult ExecuteInternal(ChartStep step, ParameterBag parameters, bool isRedo)
        {
            ChartDefinition definition = new ChartDefinition { ChartType = step.ChartType };
            // Series: First X...
            string dataName = null;
            if (null != step.XSeriesDataName)
            {
                if (!parameters.ContainsKey(step.XSeriesDataName))
                    throw new Exception("Chart expected parameter \"" + step.XSeriesDataName + "\", which was not supplied");
                DataFrame frame = parameters[step.XSeriesDataName].AsDataFrame;
                for (int v = 0; v < frame.VariableCount; v++)
                {
                    DoubleVariable variable = frame.Variables[v].AsDoubleVariable;
                    definition.AddXSeriesAt(VariableToSeries(variable), v);
                }
                dataName = frame.Name;
            }
            // ... then Y
            if (null != step.YSeriesDataName)
            {
                if (!parameters.ContainsKey(step.YSeriesDataName))
                    throw new Exception("Chart expected parameter \"" + step.YSeriesDataName + "\", which was not supplied");
                DataFrame frame = parameters[step.YSeriesDataName].AsDataFrame;
                for (int v = 0; v < frame.VariableCount; v++)
                {
                    DoubleVariable variable = frame.Variables[v].AsDoubleVariable;
                    definition.AddYSeriesAt(VariableToSeries(variable), v);
                }
                dataName = frame.Name;
            }

            ChartOptions options = FillChartOptions(step, parameters, isRedo, definition, dataName);
            string xAxisTitle = step.XAxisTitle(this, parameters);
            string yAxisTitle = step.YAxisTitle(this, parameters);
            if (!string.IsNullOrEmpty(xAxisTitle))
            {
                options.XAxisTitle = xAxisTitle;
            }
            if (!string.IsNullOrEmpty(yAxisTitle))
            {
                options.YAxisTitle = yAxisTitle;
            }

            // Plot to metafile if ascii, text otherwise
            ParameterBag results;
            using (ChartRenderer ch = new ChartRenderer(definition) { IsAscii = step.IsAscii })
            {
                if (step.IsAscii)
                {
                    results = ch.Plot(null, host);
                    results.Add(step.ChartName, new FilledParameter(false, ch.AsAsciiRTF));
                }
                else
                {
                    string rtf;
                    results = ch.PlotAndReturnRtf(host, out rtf);
                    results.Add(step.ChartName, new FilledParameter(false, rtf));
                }
                string parameterName = STATSDIRECT_CHART_OPTIONS + (step.ChartName ?? "");
                results.Add(parameterName, new FilledParameter(true, options));
            }
            return new StepResult(StepSuccess.Success, results);
        }

        public StepResult ExecuteInternal(IterationStep step, ParameterBag parms, bool isRedo)
        {
            // Detect bounds: default 0 to 0 inclusive (1 iteration), then add any fixed values, then any variables if found.
            int lower = 0;
            int upper = 0;
            if (step.LowerBound.HasValue)
            {
                lower = step.LowerBound.Value;
            }
            if (step.UpperBound.HasValue)
            {
                upper = step.UpperBound.Value;
            }
            if (null != step.LowerBoundParameterName)
            {
                if (parms.ContainsKey(step.LowerBoundParameterName))
                    lower = parms[step.LowerBoundParameterName].AsInt32;
            }
            if (null != step.UpperBoundParameterName)
            {
                if (parms.ContainsKey(step.UpperBoundParameterName))
                    upper = parms[step.UpperBoundParameterName].AsInt32;
            }
            ParameterBag filledParameters = new ParameterBag();
            for (int i = lower; i <= upper; i++)
            {
                if (null != step.LoopVariableName)
                {
                    filledParameters[step.LoopVariableName] = new FilledParameter(false, i);
                }
                foreach (Step s in step.Steps)
                {
                    // TODO: How to handle execution failures?
                    StepResult result = s.ExecuteInternal(this, filledParameters, isRedo);
                    // Add in any required parameters, combining everything into one big mass of outputs.
                    // Overwrite earlier loop results with later ones.
                    foreach (string k in result.ParameterBag.Keys)
                        filledParameters[k] = result.ParameterBag[k];
                }
            }
            return new StepResult(StepSuccess.Success, filledParameters);
        }

        public StepResult ExecuteInternal(OutputFrameStep step, ParameterBag parameters, bool isRedo)
        {
            DataFrame frame = parameters[step.ParameterName].AsDataFrame;
            if (null != frame)
            {
                PaneAndPosition preferredPaneAndPosition = null;
                if (parameters.ContainsKey(STATSDIRECT_FRAME_PANE)
                    && null != parameters[STATSDIRECT_FRAME_PANE])
                    preferredPaneAndPosition = parameters[STATSDIRECT_FRAME_PANE].AsPaneAndPosition;
                host.OutputFrame(frame, step.KeepSelection, step.IsFormulae, step.MissingIndicator, preferredPaneAndPosition);
            }
            return new StepResult(StepSuccess.Success, new ParameterBag());
        }
        /*
        public StepResult ExecuteInternal(SelectOutputForFrameStep step, ParameterBag parameters, bool isRedo)
        {
            if ((!parameters.ContainsKey(STATSDIRECT_FRAME_PANE)) || null == parameters[STATSDIRECT_FRAME_PANE])
                host.SelectOutputForFrame();
            return new StepResult(StepSuccess.Success, new ParameterBag());
        }*/

        /// <summary>
        /// 
        /// </summary>
        /// <param name="step"></param>
        /// <param name="parms"></param>
        /// <param name="isRedo"></param>
        /// <returns></returns>
        /// <remarks>Note that this may return parameters with null values; it is up to the caller to remove these.</remarks>
        public StepResult ExecuteInternal(ParametersStep step, ParameterBag parms, bool isRedo)
        {
            // If we're redoing a previous operation, then all parameters are taken from the previous operation.  We do not request any.
            if (isRedo)
                return new StepResult(StepSuccess.Success, new ParameterBag());

            try
            {
                ParameterBag filledParameters = new ParameterBag();
                List<Parameter> outstandingParameters = new List<Parameter>();
                foreach (Parameter parameter in step.Parameters)
                {
                    // If the parameter is already present in the input bag, and we're copying the input, skip acquiring it again.
                    // This is typically due to this being a follow-on from another operation, and some parameters already being set.
                    if (step.ShouldCopyInputParameters && !parameter.MustRequest && null != parameter.Name && parms.ContainsKey(parameter.Name))
                        continue;

                    // Check prerequisites and skip this parameter if they're not met.
                    if (null != parameter.RequiresParameter)
                    {
                        // The required parameter must be present...
                        if (!filledParameters.ContainsKey(parameter.RequiresParameter))
                            continue;
                        // ... and non-blank.
                        if (null == filledParameters[parameter.RequiresParameter])
                            continue;
                    }

                    // Fill and validate the parameter
                    // Union parms with filledParameters before we pass in, so that this has access to earlier parameters in the same series
                    ParameterBag parmsAndFilledParameters = new ParameterBag();
                    foreach (KeyValuePair<string, FilledParameter> pair in filledParameters.Pairs)
                    {
                        parmsAndFilledParameters.Add(pair);
                    }
                    foreach (KeyValuePair<string, FilledParameter> pair in parms.Pairs)
                    {
                        if (!parmsAndFilledParameters.ContainsKey(pair.Key))
                            parmsAndFilledParameters.Add(pair);
                    }

                    // If we don't already have the parameter and its lifetime is something other than just this operation, see whether it's already in the session
                    if (parameter.Lifetime == ParameterLifetime.SessionForThisOperation && null != parameter.Name && !parmsAndFilledParameters.ContainsKey(parameter.Name))
                    {
                        IDictionary<string, ParameterBag> savedParametersPerOperation = host.SessionParametersPerOperation;
                        if (savedParametersPerOperation.ContainsKey(parameter.Operation.Name))
                        {
                            ParameterBag savedParameters = savedParametersPerOperation[parameter.Operation.Name];
                            if (savedParameters.ContainsKey(parameter.Name))
                            {
                                filledParameters.Add(parameter.Name, savedParameters[parameter.Name]);
                            }
                        }
                    }
                    if (parameter.Lifetime == ParameterLifetime.SessionForAllOperations && null != parameter.Name && !parmsAndFilledParameters.ContainsKey(parameter.Name))
                    {
                        ParameterBag savedParameters = host.SessionParametersAcrossOperations;
                        if (savedParameters.ContainsKey(parameter.Name))
                        {
                            filledParameters.Add(parameter.Name, savedParameters[parameter.Name]);
                        }
                    }

                    // Try to combine requests for parameters where possible.  The host can always refuse a request.
                    bool shouldCombine = host.CanCombine(parameter);
                    if (!shouldCombine)
                    {
                        // We've hit a parameter we should not or cannot combine.  Ensure any grouped parameters are handled at this point.
                        // We also know that we definitely cannot request an output window at this point - so don't!
                        if (outstandingParameters.Count > 0)
                        {
                            // Fill in and validate previous parameters
                            ParameterBag outstandingFilledParameters = host.FillAndValidateCombinedParameters(this, parmsAndFilledParameters);
                            if (null == outstandingFilledParameters)
                                throw new TemplateOperationCancelledException();
                            foreach (Parameter outstandingParameter in outstandingParameters)
                                MaybeRemember(outstandingParameter, outstandingFilledParameters);
                            foreach (KeyValuePair<string, FilledParameter> pair in outstandingFilledParameters.Pairs)
                            {
                                // Handle removal of explicit blanks
                                if (null == pair.Value)
                                {
                                    // Don't copy this parameter over; but remove it from filledParameters if present.
                                    if (filledParameters.ContainsKey(pair.Key))
                                        filledParameters.Remove(pair.Key);
                                }
                                else
                                    filledParameters[pair.Key] = pair.Value;
                            }
                            outstandingParameters.Clear();
                        }
                    }
                    ParameterBag newFilledParameters = host.FillParameter(this, parameter, parmsAndFilledParameters, shouldCombine);
                    if (shouldCombine)
                    {
                        outstandingParameters.Add(parameter);
                    }
                    else
                    {
                        MaybeRemember(parameter, newFilledParameters);
                        if (null != newFilledParameters)
                        {
                            foreach (KeyValuePair<string, FilledParameter> pair in newFilledParameters.Pairs)
                            {
                                filledParameters.Add(pair.Key, pair.Value);
                            }
                        }
                    }
                }

                // We've reached the end of the list.  Ensure any grouped parameters are handled at this point.
                // We might also be able to request output frame or report parameters now, if the operation doesn't do anything else.
                if (outstandingParameters.Count > 0)
                {
                    // Union parms with filledParameters before we pass in, so that this has access to earlier parameters in the same series
                    ParameterBag parmsAndFilledParameters = new ParameterBag();
                    foreach (KeyValuePair<string, FilledParameter> pair in filledParameters.Pairs)
                    {
                        parmsAndFilledParameters.Add(pair);
                    }
                    foreach (KeyValuePair<string, FilledParameter> pair in parms.Pairs)
                    {
                        if (!parmsAndFilledParameters.ContainsKey(pair.Key))
                            parmsAndFilledParameters.Add(pair);
                    }

                    Step frameStep;
                    Step outputForFrameStep;
                    if ((step.Operation.ShouldRequestTargetAfter(step, Step.StepType.Frame, out frameStep) == HasInput.NoAndTypeFound)
                        || (step.Operation.ShouldRequestTargetAfter(step, Step.StepType.SelectOutputForFrame, out outputForFrameStep) == HasInput.NoAndTypeFound))
                    {
                        bool preferInPlaceInsertion = null != frameStep && ((OutputFrameStep)frameStep).PreferInPlaceInsertion;
                        string missingIndicator = null == frameStep ? Formatting.ASTERISK : ((OutputFrameStep)frameStep).MissingIndicator;
                        SpecialParameter frameParameter = new SpecialParameter { Name = STATSDIRECT_FRAME_PANE, SpecialType = "frame", ExtraData = new object[] { preferInPlaceInsertion, missingIndicator } };
                        host.FillParameter(this, frameParameter, parmsAndFilledParameters, true);
                    }

                    // Handle previous parameter fill-in, validation and combination
                    ParameterBag outstandingFilledParameters = host.FillAndValidateCombinedParameters(this, parmsAndFilledParameters);
                    if (null == outstandingFilledParameters)
                        throw new TemplateOperationCancelledException();
                    foreach (Parameter outstandingParameter in outstandingParameters)
                    {
                        MaybeRemember(outstandingParameter, outstandingFilledParameters);
                    }
                    foreach (KeyValuePair<string, FilledParameter> pair in outstandingFilledParameters.Pairs)
                        filledParameters[pair.Key] = pair.Value;
                    outstandingParameters.Clear();
                }
                return new StepResult(StepSuccess.Success, filledParameters);
            }
            catch (Utilities.TemplateOperationCancelledException)
            {
                throw;
            }
#if PRODUCTION
            catch(Exception ex)
            {
                // Fail the operation
                host.Error("Internal error: " + ex.Message, "Operation terminated");
                throw new Utilities.TemplateOperationCancelledException();
            }
#endif
        }

        /// <summary>
        /// A parameter has just been acquired.  If it should be remembered for the session, remember it.
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="parameterBag"></param>
        private void MaybeRemember(Parameter parameter, ParameterBag parameterBag)
        {
            // Null parameter bags come from cancelling optional parameters.
            if (null == parameterBag)
                return;

            // Multiple option parameters have null names; don't fill these at present.
            // TODO: Fill multiple-option parameters specially
            if (null == parameter || null == parameter.Name)
                return;

            // Find the parameter to remember.  If it's not present in the bag, do nothing.
            FilledParameter filledParameterToSave;
            if (!parameterBag.TryGetValue(parameter.Name, out filledParameterToSave))
                return;

            switch (parameter.Lifetime)
            {
                case ParameterLifetime.SessionForThisOperation:
                    {
                        IDictionary<string, ParameterBag> savedParametersPerOperation = host.SessionParametersPerOperation;
                        ParameterBag savedParameterBag;
                        if (!savedParametersPerOperation.TryGetValue(parameter.Operation.Name, out savedParameterBag))
                        {
                            savedParameterBag = new ParameterBag();
                            savedParametersPerOperation.Add(parameter.Operation.Name, savedParameterBag);
                        }
                        savedParameterBag[parameter.Name] = filledParameterToSave;
                    }
                    break;
                case ParameterLifetime.SessionForAllOperations:
                    {
                        ParameterBag savedParameterBag = host.SessionParametersAcrossOperations;
                        savedParameterBag[parameter.Name] = filledParameterToSave;
                    }
                    break;
            }
        }

        public StepResult ExecuteInternal(ReportStep reportStep, ParameterBag parameters, bool isRedo)
        {
            string filledReport = reportStep.Substitute(parameters);

            object /* Pane */ preferredPane = null;
            if (parameters.ContainsKey(STATSDIRECT_REPORT_PANE)
                && null != parameters[STATSDIRECT_REPORT_PANE])
                preferredPane = parameters[STATSDIRECT_REPORT_PANE].AsPane;
            string xml = null;
            try
            {
                bool shouldKeepData = host.Preferences.ShouldKeepData;
                xml = parameters.SerializeForRedo(shouldKeepData);
            }
            catch (Exception)
            {
                // TODO: Log what failed to be serialized so that it's possible to fix the problem.
            }
            preferredPane = host.OutputReport(filledReport, reportStep.Operation, xml, preferredPane);

            // Log the ID of the report that was actually used
            ParameterBag outputParameters = new ParameterBag();
            // outputParameters.Add(REPORT_ID_NAME, new FilledParameter(true, reportId));
            if (!parameters.ContainsKey(STATSDIRECT_REPORT_PANE))
                outputParameters.Add(STATSDIRECT_REPORT_PANE, new FilledParameter(true, preferredPane));
            return new StepResult(StepSuccess.Success, outputParameters);
        }

        public StepResult ExecuteInternal(ScriptStep step, ParameterBag parameters, bool isRedo)
        {
            IScriptEngine scriptEngine = host.GetScriptEngine(step.Language);
            string entryPoint = step.EntryPoint;
            ScriptType scriptType = null == entryPoint ? ScriptType.Function : ScriptType.MultipleMethods;
            return new StepResult(StepSuccess.Success, (ParameterBag)scriptEngine.Run(step.Language, step.Body, scriptType, host, parameters, null, entryPoint));
        }

        public StepResult ExecuteInternal(TestStep step, ParameterBag parms, bool isRedo)
        {
            // HACK: This is not a proper interpreter, and should be!
            bool result = (bool)Evaluate(step.Condition, parms);
            IList<Step> steps = result ? step.TrueSteps : step.FalseSteps;
            foreach (Step s in steps)
            {
                StepResult stepResult = Execute(s, parms, isRedo);
                if (null == stepResult)
                    return new StepResult(StepSuccess.Failed, new ParameterBag());
                if (stepResult.StepSuccess == StepSuccess.Failed)
                    return new StepResult(StepSuccess.Failed, new ParameterBag());
                parms = stepResult.ParameterBag;
            }
            return new StepResult(StepSuccess.Success, parms);
        }

        private DoubleSeries VariableToSeries(DoubleVariable variable)
        {
            DoubleSeries series = new DoubleSeries { Title = variable.Title, Data = variable.Data };
            return series;
        }

        public object Evaluate(Expression expression, ParameterBag parameters)
        {
            if (expression.Body.StartsWith("="))
            {
                IScriptEngine scriptEngine = host.GetScriptEngine(expression.Language);
                return scriptEngine.Run(expression.Language, expression.Body.Substring(1), ScriptType.Expression, host, parameters, null, null);
            }
            int candidateInt;
            if (Int32.TryParse(expression.Body, out candidateInt))
                return candidateInt;
            double candidateDouble;
            if (double.TryParse(expression.Body, out candidateDouble))
                return candidateDouble;
            bool candidateBoolean;
            if (bool.TryParse(expression.Body, out candidateBoolean))
                return candidateBoolean;
            DateTime candidateDateTime;
            if (DateTime.TryParse(expression.Body, out candidateDateTime))
                return candidateDateTime;
            return expression.Body;
        }

        /// <summary>
        /// for any gidx call - cdat().bins is not populated
        /// this sub calculates the bins if required e.g. by rpt_frequency
        /// </summary>
        public static ClassifierVariable gidx_bins(DoubleVariable v)
        {
            // find number of categories
            int ng = 1;
            // Space/time trade-off: never reallocate g or gin, but they're large!
            double[] g = new double[v.Length]; // There will be at most v.Length groups
            int[] gin = new int[v.Length]; // There will be at most v.Length groups
            for (int j = 0; j < v.Length; j++)
            {
                if (v.Data[j] != Constant.MISSING)
                {
                    g[0] = v.Data[j];
                    gin[0] = 1;
                    break;
                }
            }
            for (int j = 1; j < v.Length; j++)
            {
                bool newa = true;
                int mg = 0;
                if (v.Data[j] == Constant.MISSING)
                {
                    newa = false;
                }
                else
                {
                    for (int i = 0; i < ng; i++)
                    {
                        if (v.Data[j] == g[i])
                        {
                            newa = false;
                            mg = i;
                            break;
                        }
                    }
                }
                if (newa)
                {
                    g[ng] = v.Data[j];
                    gin[ng] = 1;
                    ng++;
                }
                else
                {
                    if (v.Data[j] != Constant.MISSING)
                        gin[mg]++;
                }
            }

            ClassifierVariable cv = new ClassifierVariable { Title = v.Title, Data = v.Data };
            for (int i = 0; i < ng; i++)
            {
                Group grp = new Group(g[i].ToString(), g[i]) { NBin = gin[i] };
                cv.Groups.Add(grp);
            }
            return cv;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="host"></param>
        /// <param name="validatorName"> </param>
        /// <param name="parameter"></param>
        /// <param name="filledParameters"></param>
        /// <param name="failedValidationMessage"></param>
        /// <returns>A string containing at least one validation error, or none if there are no validation errors detected.</returns>
        public static string Validate(ITemplateHost host, string validatorName, Parameter parameter, ParameterBag filledParameters, string failedValidationMessage)
        {
            // Does the operation define a custom validator with that name?  If so, use it.
            if (null != parameter.Operation.CustomValidators)
            {
                foreach (CustomValidator candidate in parameter.Operation.CustomValidators)
                {
                    if (candidate.Name.Equals(validatorName))
                    {
                        string language = candidate.Language ?? "CSharp";
                        IScriptEngine scriptEngine = host.GetScriptEngine(language);
                        object result = scriptEngine.Run(language, candidate.Script, ScriptType.Validator, host, filledParameters, parameter, null);
                        if (null == result)
                            return null;
                        return result.ToString();
                    }
                }
            }
            // If there's no custom validator with that name, use a generic if we have one.
            switch (validatorName)
            {
                case "Pooling":
                    {
                        // If we're allowing blank parameters, accept a blank
                        if ((null != parameter.CancelSkipsParameter) && (null == filledParameters || 0 == filledParameters.Count))
                            return null;

                        // Otherwise ensure all values are in {-1, 0, 1}
                        DataFrame dataFrame = filledParameters[parameter.Name].AsDataFrame;
                        DoubleVariable variable = dataFrame.Variables[0].AsDoubleVariable;
                        foreach (double value in variable.Data)
                        {
                            if (0 != value && -1 != value && 1 != value)
                                return failedValidationMessage ?? "Pooling indicator must be 0 (not pooled), 1 (subgroup) or -1 (pooled) only";
                        }
                    }
                    return null;
                case "Square":
                    {
                        // If we're allowing blank parameters, accept a blank
                        if ((null != parameter.CancelSkipsParameter) && (null == filledParameters || 0 == filledParameters.Count))
                            return null;

                        // Otherwise ensure the number of values is a square number
                        DataFrame dataFrame = filledParameters[parameter.Name].AsDataFrame;
                        DoubleVariable variable = dataFrame.Variables[0].AsDoubleVariable;
                        if (Math.Sqrt(variable.Length) != Math.Floor(Math.Sqrt(variable.Length)))
                        {
                            return failedValidationMessage ?? "Number of observations can not be arranged as a square (i.e. integer square root)";
                        }
                    }
                    return null;
                case "SquareBins":
                    {
                        // If we're allowing blank parameters, accept a blank
                        if ((null != parameter.CancelSkipsParameter) && (null == filledParameters || 0 == filledParameters.Count))
                            return null;

                        // Otherwise ensure the number of bins is the square root of the number of values
                        DataFrame dataFrame = filledParameters[parameter.Name].AsDataFrame;
                        DoubleVariable variable = dataFrame.Variables[0].AsDoubleVariable;
                        ClassifierVariable cv = gidx_bins(variable);
                        if (Math.Sqrt(variable.Length) != cv.GroupCount)
                        {
                            return failedValidationMessage ?? "There should be " + Math.Sqrt(variable.Length).ToString("N0") + " classes";
                        }
                    }
                    return null;
                case "CheckForNonDummiedCategories":
                    {
                        // If we're allowing blank parameters, accept a blank
                        if ((null != parameter.CancelSkipsParameter) && (null == filledParameters || 0 == filledParameters.Count))
                            return null;

                        // Otherwise ensure the number of bins, if >2, is at least 12 (or they're all distinct)
                        DataFrame frame = filledParameters[parameter.Name].AsDataFrame;
                        foreach (DoubleVariable v in frame.Variables)
                        {
                            double[] data = v.Data;
                            bool skip = false;
                            // skip if all not integers
                            foreach (double t in data)
                            {
                                if (t != Math.Floor(t))
                                {
                                    skip = true;
                                    break;
                                }
                            }
                            if (!skip)
                            {
                                // get number of categories if all integers
                                int ng = 1;
                                int[] g = new int[data.Length];
                                foreach (double t in data)
                                {
                                    if (t != Constant.MISSING)
                                    {
                                        g[0] = (int)t;
                                        break;
                                    }
                                }
                                for (int j = 1; j < data.Length; j++)
                                {
                                    bool newa = true;
                                    for (int i = 0; i < ng; i++)
                                    {
                                        if (data[j] == g[i] || data[j] == Constant.MISSING)
                                        {
                                            newa = false;
                                            break;
                                        }
                                    }
                                    if (newa)
                                    {
                                        g[ng++] = (int)data[j];
                                    }
                                }
                                if (ng > 2 && ng < Math.Min(data.Length - 2, 12))
                                {
                                    bool wasCancelled;
                                    bool sortOutData = host.GetBoolean("The variable named '" + v.Title + "' seems to contain categorical data.\r\n\r\nIf you want to use categorical data containing more than two categories,\r\nthen please use the 'Data_Dummy Variables' menu item to convert this variable\r\nto dummy variables before running the regression again.\r\n\r\nDo you want to quit this regression and sort out your data?", "Regression Predictor Scan", false, 140766, out wasCancelled);
                                    if (wasCancelled || sortOutData)
                                        throw new TemplateOperationCancelledException();
                                }
                            }
                        }
                        // If we get here, we're fine
                    }
                    return null;
                case "Boolean":
                    {
                        // If we're allowing blank parameters, accept a blank
                        if ((null != parameter.CancelSkipsParameter) && (null == filledParameters || 0 == filledParameters.Count))
                            return null;

                        // Otherwise ensure all values are in {0, 1}
                        DataFrame dataFrame = filledParameters[parameter.Name].AsDataFrame;
                        foreach (Variable variable in dataFrame.Variables)
                        {
                            if (variable is DoubleVariable)
                            {
                                DoubleVariable doubleVariable = variable.AsDoubleVariable;
                                foreach (double value in doubleVariable.Data)
                                {
                                    if (0.0 != value && 1.0 != value)
                                    {
                                        return failedValidationMessage ?? "Case-control indicator must be 1 for case or 0 for control only";
                                    }
                                }
                            }
                        }
                    }
                    return null;
                case "Positive":
                    {
                        // If we're allowing blank parameters, accept a blank
                        if ((null != parameter.CancelSkipsParameter) && (null == filledParameters || 0 == filledParameters.Count))
                            return null;

                        // Otherwise ensure all values are > 0
                        DataFrame dataFrame = filledParameters[parameter.Name].AsDataFrame;
                        foreach (Variable variable in dataFrame.Variables)
                        {
                            if (variable is DoubleVariable)
                            {
                                DoubleVariable doubleVariable = variable.AsDoubleVariable;
                                foreach (double value in doubleVariable.Data)
                                {
                                    if (value <= 0)
                                    {
                                        return failedValidationMessage ?? "Data must be positive non-zero numbers";
                                    }
                                }
                            }
                        }
                    }
                    return null;
                case "NonNegative":
                    {
                        // If we're allowing blank parameters, accept a blank
                        if ((null != parameter.CancelSkipsParameter) && (null == filledParameters || 0 == filledParameters.Count))
                            return null;

                        // Otherwise ensure all values are >= 0
                        DataFrame dataFrame = filledParameters[parameter.Name].AsDataFrame;
                        foreach (Variable variable in dataFrame.Variables)
                        {
                            if (variable is DoubleVariable)
                            {
                                DoubleVariable doubleVariable = variable.AsDoubleVariable;
                                foreach (double value in doubleVariable.Data)
                                {
                                    if (value < 0)
                                    {
                                        return failedValidationMessage ?? "Data must be positive or zero numbers";
                                    }
                                }
                            }
                        }
                    }
                    return null;
                case "ZeroToOneExclusive":
                    {
                        // If we're allowing blank parameters, accept a blank
                        if ((null != parameter.CancelSkipsParameter) && (null == filledParameters || 0 == filledParameters.Count))
                            return null;

                        // Otherwise ensure all values are in the range (0, 1)
                        DataFrame dataFrame = filledParameters[parameter.Name].AsDataFrame;
                        foreach (Variable variable in dataFrame.Variables)
                        {
                            if (variable is DoubleVariable)
                            {
                                DoubleVariable doubleVariable = variable.AsDoubleVariable;
                                foreach (double value in doubleVariable.Data)
                                {
                                    if (value <= 0 || value >= 1)
                                    {
                                        return failedValidationMessage ?? "Data must lie between 0 and 1";
                                    }
                                }
                            }
                        }
                    }
                    return null;
                case "TwoBinsAndNoMissingData":
                    {
                        // If we're allowing blank parameters, accept a blank
                        if ((null != parameter.CancelSkipsParameter) && (null == filledParameters || 0 == filledParameters.Count))
                            return null;

                        // Otherwise ensure there are exactly two bins in the classifier variable
                        DataFrame dataFrame = filledParameters[parameter.Name].AsDataFrame;
                        ClassifierVariable variable = dataFrame.Variables[0].AsClassifierVariable;
                        if (variable.GroupCount != 2)
                        {
                            return failedValidationMessage ?? "Group identifier must contain two groups and no missing data";
                        }
                    }
                    return null;
                case "NoMissingData":
                    {
                        // If we're allowing blank parameters, accept a blank
                        if ((null != parameter.CancelSkipsParameter) && (null == filledParameters || 0 == filledParameters.Count))
                            return null;

                        // Otherwise ensure there's no missing data in any of the numeric variables in the frame
                        DataFrame dataFrame = filledParameters[parameter.Name].AsDataFrame;
                        foreach (Variable variable in dataFrame.Variables)
                        {
                            if (variable is DoubleVariable)
                            {
                                DoubleVariable doubleVariable = variable.AsDoubleVariable;
                                foreach (double value in doubleVariable.Data)
                                {
                                    if (value == Constant.MISSING)
                                    {
                                        return failedValidationMessage ?? "Operation cannot take missing data";
                                    }
                                }
                            }
                        }
                    }
                    return null;
                case "PersonTimeSize":
                    {
                        // Assumes no missing data, no data < 0
                        DataFrame datFrame = filledParameters["data"].AsDataFrame;
                        DoubleVariable datV0 = datFrame.Variables[0].AsDoubleVariable;
                        DoubleVariable datV1 = datFrame.Variables[1].AsDoubleVariable;
                        DoubleVariable datV2 = datFrame.Variables[2].AsDoubleVariable;
                        int rows = datFrame.MaxRows;

                        double refntot = 0.0;
                        for (int j = 1; j <= rows; j++)
                        {
                            double xy = datV0.Data[j - 1];
                            double xn = datV1.Data[j - 1];
                            double rf = datV2.Data[j - 1];
                            refntot += rf;
                            if (xn <= 0)
                                return "Person-time must be greater than zero";
                            if (xy > xn)
                                return "Number of events must be greater then person-time, do not scale person-time";
                        }

                        if (refntot <= 0.0)
                            return "Total reference group size must be greater than zero";
                    }
                    return null;
                default:
                    throw new ArgumentOutOfRangeException("validatorName", validatorName, "No validator with the specified name");
            }
        }

    }
}
