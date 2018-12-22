using System;
using System.Collections.Generic;
using System.Diagnostics;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.Charting
{
    public static class ChartOptionProcessor
    {
        public static ChartOptions PreprocessChartOptions(ChartStep step, ParameterBag parameters, ChartDefinition definition, string dataName, ITemplateHost host)
        {
            // Chart options
            // TODO: This is very poor placement of this logic.  It's an unpleasant mash of setting options (some of which should be defaults), data preparation and mapping from values in particular operations.  How much of this should be moved out to the XML?
            switch (step.ChartType)
            {
                case ChartType.AgreementPair:
                    return PreprocessAgreementOptions(host, step, parameters);
                case ChartType.Bar:
                case ChartType.StackedBar:
                case ChartType.StackedBar100Percent:
                    return PreprocessBarOptions(host, step, parameters, definition, dataName);
                case ChartType.BiasMA:
                    return PreprocessBiasMAOptions(host, step, definition, dataName);
                case ChartType.BoxWhisker:
                    return PreprocessBoxWhiskerOptions(host, step, definition, dataName);
                case ChartType.Control:
                    return PreprocessControlOptions(host, definition, dataName);
                case ChartType.ErrorBar:
                    return PreprocessErrorBarOptions(host, parameters, dataName);
                case ChartType.Forest:
                    return PreprocessForestOptions(host, parameters, definition, dataName);
                case ChartType.Gini:
                    return PreprocessGiniOptions(host, definition, dataName);
                case ChartType.Histogram:
                    return PreprocessHistogramOptions(host, step, definition);
                case ChartType.Ladder:
                    return PreprocessLadderOptions(host, definition, dataName);
                case ChartType.LineXY:
                    return PreprocessLineXYOptions(host, definition, dataName);
                case ChartType.LinearRegression:
                    return PreprocessLinearRegressionOptions(host, step, parameters);
                case ChartType.Normal:
                    return PreprocessNormalOptions(host, parameters, dataName);
                case ChartType.Pyramid:
                    return PreprocessPyramidOptions(host, parameters, dataName);
                case ChartType.ScatterXY:
                    return PreprocessScatterXYOptions(host, step, definition, dataName);
                case ChartType.ROC:
                    return PreprocessRocOptions(host, parameters, definition);
                case ChartType.Spread:
                    return PreprocessSpreadOptions(host, definition, dataName);
                case ChartType.Survival:
                    return PreprocessSurvivalOptions(host, parameters, dataName);
                default:
                    throw new ArgumentOutOfRangeException(nameof(step), step, "step.ChartType: Not all types can be plotted yet");
            }
        }

        private static ChartOptions PreprocessBiasMAOptions(ITemplateHost host, ChartStep step, ChartDefinition definition, string dataName)
        {
            // Nothing required
            return null;
        }

        private static SurvivalOptions PreprocessSurvivalOptions(ITemplateHost host, ParameterBag parameters, string dataName)
        {
            SurvivalOptions survivalOptions = new SurvivalOptions(host.Preferences.ShouldUseColour)
            {
                ShouldAutoscale =
                    !ChartPreferences.DefaultRequestScaleLimits,
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
                ydatlFrame = parameters["ydatl"].AsDataFrame;

            DataFrame ydatuFrame = null;
            if (parameters.ContainsKey("ydatu") && null != parameters["ydatu"])
                ydatuFrame = parameters["ydatu"].AsDataFrame;

            for (int i = 0; i < groupCount; i++)
            {
                SurvivalOptions.SurvivalSeries ser = new SurvivalOptions.SurvivalSeries();
                // Series: xdat...
                DoubleVariable xdatVariable = (DoubleVariable)xdatFrame.Variables[i];
                ser.XDat = xdatVariable.Data;
                // ... cdat...
                DoubleVariable cdatVariable = (DoubleVariable)cdatFrame.Variables[i];
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
                DoubleVariable ydatVariable = (DoubleVariable)ydatFrame.Variables[i];
                ser.YDat = ydatVariable.Data;
                // ... ydatl...
                if (null != ydatlFrame)
                {
                    DoubleVariable ydatlVariable = (DoubleVariable)ydatlFrame.Variables[i];
                    ser.YDatL = ydatlVariable.Data;
                }
                // ... and ydatu
                if (null != ydatuFrame)
                {
                    DoubleVariable ydatuVariable = (DoubleVariable)ydatuFrame.Variables[i];
                    ser.YDatU = ydatuVariable.Data;
                }
                survivalOptions.Series.Add(ser);
            }
            survivalOptions.SeriesTitles = new string[groupCount];
            survivalOptions.ShowEventMarkers = true;
            survivalOptions.YAxisTitle = "Survival proportion";
            for (int i = 0; i < groupCount; i++)
                survivalOptions.SeriesTitles[i] = (i + 1).ToString();
            survivalOptions.SetMarkers();
            survivalOptions.ShowLegend = groupCount > 1;
            return survivalOptions;
        }

        private static SpreadOptions PreprocessSpreadOptions(ITemplateHost host, ChartDefinition definition, string dataName)
        {
            // If only X series have been passed in, we're vertical.  If only Y, we're horizontal.  If both or neither, we can't plot.
            if (0 == definition.XSeries.Count && 0 == definition.YSeries.Count)
                throw new ArgumentException("Must have at least one series to plot a spread plot");
            if (definition.XSeries.Count > 0 && definition.YSeries.Count > 0)
                throw new ArgumentException("Cannot plot a spread plot with both X and Y series");
            IList<ISeries> seriesToUse = definition.YSeries.Count > 0 ? definition.YSeries : definition.XSeries;

            SpreadOptions spreadOptions = new SpreadOptions(host.Preferences.ShouldUseColour)
            {
                ShouldAutoscale = !ChartPreferences.DefaultRequestScaleLimits,
                Title =
                    null == dataName
                        ? "Spread plot"
                        : "Spread plot from " + dataName,
                SeriesTitles = new string[seriesToUse.Count]
            };
            for (int i = 0; i < seriesToUse.Count; i++)
                spreadOptions.SeriesTitles[i] = seriesToUse[i].Title;
            return spreadOptions;
        }

        private static ROCOptions PreprocessRocOptions(ITemplateHost host, ParameterBag parameters, ChartDefinition definition)
        {
            if (!parameters.ContainsKey("series-count"))
                throw new Exception("Chart expected parameter \"series-count\", which was not supplied");
            int seriesCount = parameters["series-count"].AsInt32;
            // Series: First present...
            if (!parameters.ContainsKey("P"))
                throw new Exception("Chart expected parameter \"P\", which was not supplied");
            DataFrame frame = parameters["P"].AsDataFrame;
            for (int v = 0; v < frame.VariableCount; v++)
            {
                DoubleVariable variable = frame.Variables[v] as DoubleVariable;
                definition.AddXSeriesAt(VariableToSeries(variable), v);
            }
            // ... then absent
            if (!parameters.ContainsKey("A"))
                throw new Exception("Chart expected parameter \"A\", which was not supplied");
            frame = parameters["A"].AsDataFrame;
            for (int v = 0; v < frame.VariableCount; v++)
            {
                DoubleVariable variable = frame.Variables[v] as DoubleVariable;
                definition.AddYSeriesAt(VariableToSeries(variable), v);
            }
            string dataName = frame.Name;
            double pmn = 1;
            double amn = 1;
            for (int c = 0; c < definition.XSeries.Count; c++)
            {
                DoubleSeries xs = (DoubleSeries)definition.XSeries[c];
                DoubleSeries ys = (DoubleSeries)definition.YSeries[c];
                pmn = xs.Sum / xs.Points;
                amn = ys.Sum / ys.Points;
            }

            ROCOptions rocOptions = new ROCOptions(host.Preferences.ShouldUseColour, definition.XSeries)
            {
                ShouldAutoscale = !ChartPreferences.DefaultRequestScaleLimits,
                Title =
                    null == dataName ? "ROC plot" : "ROC plot from " + dataName,
                ShowCutOffCalculator = true,
                ShowOptimumCutOff = true,
                Weight = 1.0,
                GAMMA = host.Preferences.DefaultConfidenceInterval,
                Showopts = pmn > amn ? ComparisonValue.GE : ComparisonValue.LE
            };

            if (parameters.ContainsKey("GAMMA"))
                rocOptions.GAMMA = parameters["GAMMA"].AsDouble;

            rocOptions.SeriesTitles = new string[seriesCount];
            for (int i = 0; i < seriesCount; i++)
                rocOptions.SeriesTitles[i] = definition.XSeries[i].Title + " (+ve), " + definition.YSeries[i].Title + " (-ve)";
            return rocOptions;
        }

        private static ScatterXYOptions PreprocessScatterXYOptions(ITemplateHost host, ChartStep step, ChartDefinition definition, string dataName)
        {
            ScatterXYOptions sOptions = new ScatterXYOptions(host.Preferences.ShouldUseColour, definition.XSeries, false)
            {
                IsAscii = step.IsAscii,
                ShouldAutoscale = !ChartPreferences.DefaultRequestScaleLimits,
                Title = string.IsNullOrWhiteSpace(step.ChartTitle) ? (
                    null == dataName
                        ? "Scatter plot"
                        : "Scatter plot from " + dataName)
                    : step.ChartTitle,
                YAxisTitle = definition.YSeries[0].Title,
                XAxisTitle = definition.XSeries[0].Title,
                SeriesTitles = new string[definition.XSeries.Count]
            };
            for (int i = 0; i < definition.XSeries.Count; i++)
                sOptions.SeriesTitles[i] = definition.XSeries[i].Title;
            return sOptions;
        }

        private static PyramidOptions PreprocessPyramidOptions(ITemplateHost host, ParameterBag parameters, string dataName)
        {
            PyramidOptions pOptions = new PyramidOptions(host.Preferences.ShouldUseColour) { ShouldAutoscale = !ChartPreferences.DefaultRequestScaleLimits };
            if (parameters.ContainsKey("male"))
                pOptions.MaleFrame = parameters["male"].AsDataFrame;
            if (parameters.ContainsKey("female"))
                pOptions.FemaleFrame = parameters["female"].AsDataFrame;
            if (parameters.ContainsKey("labels"))
                pOptions.LabelFrame = parameters["labels"].AsDataFrame;
            pOptions.Title = null == dataName ? "Population pyramid" : "Population pyramid from " + dataName;
            pOptions.SetOptions();
            return pOptions;
        }

        private static NormalOptions PreprocessNormalOptions(ITemplateHost host, ParameterBag parameters, string dataName)
        {
            NormalOptions nOptions = new NormalOptions(host.Preferences.ShouldUseColour)
            {
                ShouldAutoscale = !ChartPreferences.DefaultRequestScaleLimits,
                Method = parameters.ContainsKey("ScoreMethod")
                             ? (NormalOptions.ScoreMethod)Parsing.Cint_Txt(parameters["ScoreMethod"].AsString)
                             : NormalOptions.ScoreMethod.VanDerWaerden,
                Title =
                    null == dataName
                        ? "Normal plot"
                        : "Normal plot from " + dataName
            };
            return nOptions;
        }

        private static LinearRegressionOptions PreprocessLinearRegressionOptions(ITemplateHost host, ChartStep step, ParameterBag parameters)
        {
            LinearRegressionOptions lrOptions = new LinearRegressionOptions(host.Preferences.ShouldUseColour)
            {
                Slope = parameters["mdnValue"].AsDouble,
                Intercept = parameters["interceptValue"].AsDouble,
                FullWidth =
                    parameters.ContainsKey("chartIsFullWidth") &&
                    parameters["chartIsFullWidth"].AsBoolean,
                Title = step.ChartTitle,
                XAxisTitle = parameters["xtitle"].AsString,
                YAxisTitle = parameters["ytitle"].AsString
            };
            return lrOptions;
        }

        private static ScatterXYOptions PreprocessLineXYOptions(ITemplateHost host, ChartDefinition definition, string dataName)
        {
            ScatterXYOptions sOptions = new ScatterXYOptions(host.Preferences.ShouldUseColour, definition.XSeries, true)
            {
                ShouldAutoscale = !ChartPreferences.DefaultRequestScaleLimits,
                Title =
                    null == dataName
                        ? "Line plot"
                        : "Line plot from " + dataName,
                YAxisTitle = definition.YSeries[0].Title,
                XAxisTitle = definition.XSeries[0].Title,
                PlotMarkers = true,
                SeriesTitles = new string[definition.XSeries.Count]
            };
            for (int i = 0; i < definition.XSeries.Count; i++)
                sOptions.SeriesTitles[i] = definition.XSeries[i].Title;
            return sOptions;
        }

        private static LadderOptions PreprocessLadderOptions(ITemplateHost host, ChartDefinition definition, string dataName)
        {
            // We need exactly 2 Y series
            if (2 != definition.YSeries.Count || 0 != definition.XSeries.Count)
                throw new ArgumentException("Must have exactly two Y series for a ladder plot");

            LadderOptions ladderOptions = new LadderOptions(host.Preferences.ShouldUseColour)
            {
                ShouldAutoscale = !ChartPreferences.DefaultRequestScaleLimits,
                Title =
                    null == dataName
                        ? "Ladder plot"
                        : "Ladder plot from " + dataName,
                SeriesTitles = new string[definition.YSeries.Count]
            };

            for (int i = 0; i < definition.YSeries.Count; i++)
                ladderOptions.SeriesTitles[i] = definition.YSeries[i].Title;
            return ladderOptions;
        }

        private static HistogramOptions PreprocessHistogramOptions(ITemplateHost host, ChartStep step, ChartDefinition definition)
        {
            IList<ISeries> series = definition.XSeries.Count > 0 ? definition.XSeries : definition.YSeries;
            HistogramOptions hOptions = new HistogramOptions(host.Preferences.ShouldUseColour)
            {
                IsAscii = step.IsAscii,
                BinChoiceMethod = step.IsAscii ? BinChoiceMethod.OldStatsDirect : BinChoiceMethod.Doane,
                LineWidth = 2,
                HistoSeriesOptions = new List<HistogramSeriesOptions>(series.Count)
            };

            // Series
            foreach (ISeries t in series)
            {
                HistogramSeriesOptions hso = new HistogramSeriesOptions
                {
                    ChartTitle = "Distribution of " + t.Title,
                    YAxisTitle = "Counts",
                    XAxisTitle = "Mid-points for " + t.Title
                };
                hOptions.HistoSeriesOptions.Add(hso);
            }
            return hOptions;
        }

        private static GiniOptions PreprocessGiniOptions(ITemplateHost host, ChartDefinition definition, string dataName)
        {
            GiniOptions giniOptions = new GiniOptions(host.Preferences.ShouldUseColour)
            {
                ShouldAutoscale = !ChartPreferences.DefaultRequestScaleLimits,
                Title =
                    null == dataName
                        ? "Lorenz plot"
                        : "Lorenz plot for " + dataName
            };

            // We need exactly one of each series
            if (1 != definition.YSeries.Count || 1 != definition.XSeries.Count)
                throw new ArgumentException("Must have exactly one X series and one Y series for a Gini chart");
            return giniOptions;
        }

        private static ForestOptions PreprocessForestOptions(ITemplateHost host, ParameterBag parameters, ChartDefinition definition, string dataName)
        {
            ForestOptions fOptions = new ForestOptions(host.Preferences.ShouldUseColour)
            {
                ShouldAutoscale = !ChartPreferences.DefaultRequestScaleLimits,
                Title =
                    null == dataName
                        ? "Forest plot"
                        : "Forest plot from " + dataName,
                k = parameters["odds"].AsDataFrame.Variables[0].Length,
                OddsRatios = ((DoubleVariable)parameters["odds"].AsDataFrame.Variables[0]).Data,
                OddsRatioLcis = ((DoubleVariable)parameters["lci"].AsDataFrame.Variables[0]).Data,
                OddsRatioUcis = ((DoubleVariable)parameters["uci"].AsDataFrame.Variables[0]).Data
            };
            if (parameters.ContainsKey("gn") && null != parameters["gn"])
            {
                fOptions.gn = ((DoubleVariable)parameters["gn"].AsDataFrame.Variables[0]).Data;
            }
            else
            {
                double[] gn = new double[fOptions.k];
                for (int i = 0; i < fOptions.k; i++)
                    gn[i] = 10;
                fOptions.gn = gn;
            }
            if (parameters.ContainsKey("pg") && null != parameters["pg"])
                fOptions.pg = ((DoubleVariable)parameters["pg"].AsDataFrame.Variables[0]).Data;
            fOptions.XAxisTitle = parameters["odds"].AsDataFrame.Variables[0].Title + " (95% confidence interval)";
            if (parameters.ContainsKey("title") && null != parameters["title"])
            {
                fOptions.Titles = ((StringVariable)parameters["title"].AsDataFrame.Variables[0]).Data;
            }
            else
            {
                string[] titles = new string[fOptions.k];
                for (int i = 0; i < fOptions.k; i++)
                    titles[i] = "stratum " + (i + 1);
                fOptions.Titles = titles;
            }

            // Sort out candidate decimal places
            double absmin = double.MaxValue;
            for (int i = 0; i < fOptions.k; i++)
            {
                if (Math.Abs(fOptions.OddsRatios[i]) < absmin && fOptions.OddsRatios[i] != 0.0)
                    absmin = Math.Abs(fOptions.OddsRatios[i]);
                if (Math.Abs(fOptions.OddsRatioLcis[i]) < absmin && fOptions.OddsRatioLcis[i] != 0.0)
                    absmin = Math.Abs(fOptions.OddsRatioLcis[i]);
                if (Math.Abs(fOptions.OddsRatioUcis[i]) < absmin && fOptions.OddsRatioUcis[i] != 0.0)
                    absmin = Math.Abs(fOptions.OddsRatioUcis[i]);
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
            return fOptions;
        }

        private static ErrorBarOptions PreprocessErrorBarOptions(ITemplateHost host, ParameterBag parameters, string dataName)
        {
            if (!parameters.ContainsKey("xdat"))
                throw new Exception("Chart expected parameter \"xdat\", which was not supplied");
            if (!parameters.ContainsKey("ydat"))
                throw new Exception("Chart expected parameter \"ydat\", which was not supplied");
            if (!parameters.ContainsKey("ydatl"))
                throw new Exception("Chart expected parameter \"ydatl\", which was not supplied");
            if (!parameters.ContainsKey("ydatu"))
                throw new Exception("Chart expected parameter \"ydatu\", which was not supplied");

            DataFrame xdatFrame = parameters["xdat"].AsDataFrame;
            DataFrame ydatFrame = parameters["ydat"].AsDataFrame;
            DataFrame ydatlFrame = parameters["ydatl"].AsDataFrame;
            DataFrame ydatuFrame = parameters["ydatu"].AsDataFrame;

            List<MultiDoubleSeries> allSeries = new List<MultiDoubleSeries>(xdatFrame.VariableCount);

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
                    MultiDoublePoint p = new MultiDoublePoint { X = noMissings.ArraysWithMissingRowsRemoved[0][i] };
                    // Ordinate
                    // Y values for error bars: [0] is centre, [1] is lower bound, [2] is upper bound.
                    p.set_Y(0, noMissings.ArraysWithMissingRowsRemoved[1][i]);
                    p.set_Y(1, noMissings.ArraysWithMissingRowsRemoved[2][i]);
                    p.set_Y(2, noMissings.ArraysWithMissingRowsRemoved[3][i]);
                    data[i] = p;
                }
                allSeries.Add(new MultiDoubleSeries { Title = seriesTitle, Data = data });
            }

            ErrorBarOptions errorBarOptions = new ErrorBarOptions(host.Preferences.ShouldUseColour)
            {
                ShouldAutoscale =
                    !ChartPreferences.DefaultRequestScaleLimits,
                Title = null == dataName
                    ? "Error bar plot"
                    : "Error bar plot plot from " + dataName,
                Series = allSeries,
                SeriesTitles = new string[ydatFrame.VariableCount],
                YAxisTitle = ydatFrame.Variables[0].Title,
                XAxisTitle = xdatFrame.Variables[0].Title
            };
            for (int i = 0; i < ydatFrame.VariableCount; i++)
            {
                errorBarOptions.SeriesTitles[i] =
                    string.IsNullOrEmpty(ydatFrame.Variables[i].Title)
                        ? "Series " + (i + 1)
                        : ydatFrame.Variables[i].Title;
            }
            errorBarOptions.SetMarkers();
            return errorBarOptions;
        }

        private static ControlOptions PreprocessControlOptions(ITemplateHost host, ChartDefinition definition, string dataName)
        {
            ControlOptions controlOptions = new ControlOptions(host.Preferences.ShouldUseColour)
            {
                ShouldBoxAxes = ChartPreferences.DefaultBoxAxes,
                ShouldAutoscale =
                    !ChartPreferences.DefaultRequestScaleLimits,
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

            controlOptions.ObservationsToUse = rows;

            controlOptions.YAxisTitle = definition.YSeries[0].Title;
            controlOptions.XAxisTitle = definition.XSeries[0].Title;
            controlOptions.UseMean = true;
            controlOptions.Use1SD = true;
            controlOptions.Use2SD = true;
            controlOptions.Use3SD = true;
            return controlOptions;
        }

        private static BoxWhiskerOptions PreprocessBoxWhiskerOptions(ITemplateHost host, ChartStep step, ChartDefinition definition, string dataName)
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
            IList<ISeries> seriesToUse = definition.YSeries.Count > 0 ? definition.YSeries : definition.XSeries;
            bwOptions.Orientation = definition.YSeries.Count > 0 ? ChartOrientation.Horizontal : ChartOrientation.Vertical;

            bwOptions.SeriesTitles = new string[seriesToUse.Count];
            for (int i = 0; i < seriesToUse.Count; i++)
                bwOptions.SeriesTitles[i] = seriesToUse[i].Title;
            bwOptions.IsAscii = step.IsAscii;
            bwOptions.MarkMeanAndMedian = false;
            bwOptions.UseInnerFence = true;
            bwOptions.UseOuterFence = true;
            return bwOptions;
        }

        private static BarOptions PreprocessBarOptions(ITemplateHost host, ChartStep step, ParameterBag parameters, ChartDefinition definition, string dataName)
        {
            BarOptions barOptions = new BarOptions(host.Preferences.ShouldUseColour)
            {
                ShouldAutoscale = !ChartPreferences.DefaultRequestScaleLimits,
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
            StringVariable labelsVariable = (StringVariable)labelsFrame.Variables[0];
            barOptions.SeriesTitles = new string[labelsVariable.Length];
            for (int i = 0; i < labelsVariable.Length; i++)
                barOptions.SeriesTitles[i] = labelsVariable.Data[i];
            barOptions.SetMarkers(definition.YSeries);
            barOptions.Orientation = ChartOrientation.Vertical;
            barOptions.ShowLegend = 1 < definition.YSeries.Count;
            if (!barOptions.ShowLegend)
                barOptions.YAxisTitle = definition.YSeries[0].Title;
            return barOptions;
        }

        private static AgreementOptions PreprocessAgreementOptions(ITemplateHost host, ChartStep step, ParameterBag parameters)
        {
            AgreementOptions aOptions = new AgreementOptions(host.Preferences.ShouldUseColour)
            {
                ShouldAutoscale = !ChartPreferences.DefaultRequestScaleLimits,
                Title = step.ChartTitle,
                lla = parameters["lla"].AsDouble,
                mean = parameters["mean"].AsDouble,
                ula = parameters["ula"].AsDouble,
                P0 = parameters["P0"].AsDouble,
                av = ((DoubleVariable)parameters["av"].AsDataFrame.Variables[0]).Data,
                mxd = ((DoubleVariable)parameters["mxd"].AsDataFrame.Variables[0]).Data
            };
            return aOptions;
        }

        public static void PostProcessFilledChartOptions(ChartDefinition definition)
        {
            switch (definition.ChartType)
            {
                case ChartType.Bar:
                case ChartType.StackedBar:
                case ChartType.StackedBar100Percent:
                    {
                        BarOptions barOptions = (BarOptions)definition.ChartOptions;
                        StringSeries labelsSeries = new StringSeries(barOptions.SeriesTitles.Length);
                        for (int i = 0; i < barOptions.SeriesTitles.Length; i++)
                            labelsSeries.Data[i] = barOptions.SeriesTitles[i];
                        definition.XSeries.Clear();
                        definition.XSeries.Add(labelsSeries);
                        break;
                    }
                case ChartType.BoxWhisker:
                    {
                        BoxWhiskerOptions bwOptions = (BoxWhiskerOptions)definition.ChartOptions;
                        IList<ISeries> seriesToUse = definition.YSeries.Count > 0 ? definition.YSeries : definition.XSeries;
                        for (int i = 0; i < seriesToUse.Count; i++)
                            seriesToUse[i].Title = bwOptions.SeriesTitles[i];
                        // Reverse series before plotting if required
                        if (bwOptions.Orientation == ChartOrientation.Horizontal && definition.XSeries.Count > 0 || bwOptions.Orientation == ChartOrientation.Vertical && definition.YSeries.Count > 0)
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
                        IList<ISeries> seriesToUse = definition.YSeries.Count > 0 ? definition.YSeries : definition.XSeries;
                        for (int i = 0; i < seriesToUse.Count; i++)
                            seriesToUse[i].Title = spreadOptions.SeriesTitles[i];
                        break;
                    }
                case ChartType.LineXY:
                case ChartType.ScatterXY:
                    {
                        ScatterXYOptions options = (ScatterXYOptions)definition.ChartOptions;
                        IList<ISeries> seriesToUse = definition.XSeries;
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

        public static DoubleSeries VariableToSeries(DoubleVariable variable)
        {
            return new DoubleSeries { Title = variable.Title, Data = variable.Data };
        }
    }
}
