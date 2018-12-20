using System;
using StatsDirect.Charting.Renderer;
using StatsDirect.Data;
using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    /// <summary>
    /// This has two purposes (TODO: one too many): know where to get the correct implementation of IChartRenderer for any given definition, and act as a one-stop shop for static calls that were previously in ChartRenderer itself to render charts that are used within the codebase.
    /// </summary>
    public static class ChartRendererFactory
    {
        // TODO: Remove EMF and RTF knowledge from this class
        public static ICanvasFactory CANVAS_FACTORY = new EmfCanvasFactory();
        public static ICanvasFactory NULL_FACTORY = new NullCanvasFactory();

        public static ChartDefinition PrepForLater(ChartType chartType, ChartOptions options)
        {
            ChartDefinition cd = new ChartDefinition { ChartType = chartType, ChartOptions = options };
            using (IChartRenderer ch = ChartRendererFor(cd, null))
                cd.ScaleParameters = ch.GetScaleParameters();
            return cd;
        }

        internal static ParameterBag PlotForResultsOnly(ITemplateHost host, ChartDefinition definition)
        {
            using (IChartRenderer ch = ChartRendererFor(definition, NULL_FACTORY))
                return ch.Plot(host, true);
        }

        // TODO: Remove all static ...AndReturnRtf from this class.
        public static string PlotLinearizedEstimationAndReturnRtf(double[] xData, double[] yData, string title, int model, double a, double b, string xAxisTitle, string yAxisTitle, bool shouldUseColour)
        {
            ChartDefinition cd = new ChartDefinition { ScaleParameters = CreateScaleParameters(ScaleType.Linear, ScaleType.Linear) };
            cd.AddYSeries(yData, yAxisTitle);
            cd.AddXSeries(xData, xAxisTitle);
            AgreementOptions aOptions = new AgreementOptions(shouldUseColour) { mxd = yData, av = xData };
            cd.ChartOptions = aOptions;
            using (ChartRenderer ch = new ChartRenderer(cd, CANVAS_FACTORY))
            {
                ch.PlotLinearizedEstimation(title, model, a, b, xAxisTitle, yAxisTitle);
                return Render(ch);
            }
        }

        public static string PlotLinearRegressionAndMaybeSeCiOrPredictionIntervalAndReturnRtf(double[] xData, double[] yData, string title, double slope, double intercept, bool fullWidth, string xAxisTitle, string yAxisTitle, double pert, int nx, double ms, double sumx, double ssx, bool isPredictionInterval)
        {
            ChartDefinition cd = new ChartDefinition {ChartType = ChartType.LinearRegression};
            DoubleSeries ys = new DoubleSeries(yData, yAxisTitle);
            cd.AddYSeriesAt(ys, 0);
            DoubleSeries xs = new DoubleSeries(xData, xAxisTitle);
            cd.AddXSeriesAt(xs, 0);
            using (LinearRegressionChartRenderer ch = (LinearRegressionChartRenderer)ChartRendererFor(cd, CANVAS_FACTORY))
            {
                double maxpcon = double.MinValue;
                double minpcon = double.MaxValue;
                if (pert != 0)
                {
                    double calcx;
                    for (calcx = xs.Min; calcx <= xs.Max; calcx += (xs.Max - xs.Min) / 20.0)
                    {
                        double calcy = slope * calcx + intercept;
                        double sey = Math.Sqrt(ms * (1.0 + (1.0 / Convert.ToDouble(nx) + Math.Pow(calcx - sumx / Convert.ToDouble(nx), 2.0) / ssx)));
                        double pconu = calcy + sey * pert;
                        double pconl = calcy - sey * pert;
                        if (pconu > maxpcon)
                            maxpcon = pconu;
                        if (pconl < minpcon)
                            minpcon = pconl;
                    }
                }

                ch.PlotLinearRegressionAndMaybeSeCiOrPredictionInterval(title, slope, intercept, fullWidth, xAxisTitle, yAxisTitle, pert, nx, ms, sumx, ssx, isPredictionInterval, Math.Min(ys.Min, minpcon), Math.Max(ys.Max, maxpcon));
                return Render(ch);
            }
        }

        public static string PlotLogitAndReturnRtf(double[] x, double[] y, string title, int model, double t, double sw, double s1, double a, double b, string xAxisTitle, string yAxisTitle, bool useLogScale)
        {
            ChartDefinition cd = new ChartDefinition { ScaleParameters = CreateScaleParameters(useLogScale ? ScaleType.Log10 : ScaleType.Linear, ScaleType.Linear) };
            cd.AddYSeries(y, yAxisTitle);
            cd.AddXSeries(x, xAxisTitle);
            using (ChartRenderer ch = new ChartRenderer(cd, CANVAS_FACTORY))
            {
                ch.PlotLogit(title, model, t, sw, s1, a, b, xAxisTitle, yAxisTitle, useLogScale);
                return Render(ch);
            }
        }

        public static string PlotPolynomialRegressionAndReturnRtf(double[] xData, double[] yData, string title, int mode, double[,] xtxi, double[] bd, double rss, int nx, int p, double gamma, string xAxisTitle, string yAxisTitle, bool shouldUseColour)
        {
            AgreementOptions aOptions = new AgreementOptions(shouldUseColour) { mxd = yData, av = xData };
            ChartDefinition cd = new ChartDefinition { ChartOptions = aOptions, ScaleParameters = CreateScaleParameters(ScaleType.Linear, ScaleType.Linear) };
            cd.AddYSeries(yData, yAxisTitle);
            cd.AddXSeries(xData, xAxisTitle);
            using (ChartRenderer ch = new ChartRenderer(cd, CANVAS_FACTORY))
            {
                ch.PlotPolynomialRegression(title, mode, xtxi, bd, rss, nx, p, gamma, xAxisTitle, yAxisTitle);
                return Render(ch);
            }
        }

        public static IChartRenderer ChartRendererFor(ChartDefinition chartDefinition, ICanvasFactory canvasFactory)
        {
            switch (chartDefinition.ChartType)
            {
                case ChartType.AgreementPair:
                    return new AgreementPairChartRenderer(chartDefinition, canvasFactory);
                case ChartType.Bar:
                    return new BarChartRenderer(chartDefinition, canvasFactory);
                case ChartType.BiasMA:
                    return new BiasMAChartRenderer(chartDefinition, canvasFactory);
                case ChartType.BoxWhisker:
                    return new BoxWhiskerChartRenderer(chartDefinition, canvasFactory);
                case ChartType.Control:
                    return new ControlChartRenderer(chartDefinition, canvasFactory);
                case ChartType.Correlation:
                    return new CorrelationChartRenderer(chartDefinition, canvasFactory);
                case ChartType.CoxSurvivalOrHazard:
                    return new CoxSurvivalOrHazardChartRenderer(chartDefinition, canvasFactory);
                case ChartType.Cox2:
                    return new Cox2ChartRenderer(chartDefinition, canvasFactory);
                case ChartType.Effect:
                    return new EffectChartRenderer(chartDefinition, canvasFactory);
                case ChartType.ErrorBar:
                    return new ErrorBarChartRenderer(chartDefinition, canvasFactory);
                case ChartType.Forest:
                    return new ForestChartRenderer(chartDefinition, canvasFactory);
                case ChartType.Gini:
                    return new GiniChartRenderer(chartDefinition, canvasFactory);
                case ChartType.Histogram:
                    return new HistogramChartRenderer(chartDefinition, canvasFactory);
                case ChartType.KaplanMeier:
                    return new KaplanMeierChartRenderer(chartDefinition, canvasFactory);
                case ChartType.LAbbe:
                    return new LAbbeChartRenderer(chartDefinition, canvasFactory);
                case ChartType.Ladder:
                    return new LadderChartRenderer(chartDefinition, canvasFactory);
                case ChartType.LinearRegression:
                    return new LinearRegressionChartRenderer(chartDefinition, canvasFactory);
                case ChartType.LineXY:
                    return new ScatterChartRenderer(chartDefinition, canvasFactory);
                case ChartType.MH:
                    return new MHChartRenderer(chartDefinition, canvasFactory);
                case ChartType.MHRD:
                    return new MHRDChartRenderer(chartDefinition, canvasFactory);
                case ChartType.Normal:
                    return new NormalChartRenderer(chartDefinition, canvasFactory);
                case ChartType.Pyramid:
                    return new PyramidChartRenderer(chartDefinition, canvasFactory);
                case ChartType.ROC:
                    return new RocChartRenderer(chartDefinition, canvasFactory);
                case ChartType.ScatterXY:
                    return new ScatterChartRenderer(chartDefinition, canvasFactory);
                case ChartType.Spread:
                    return new SpreadChartRenderer(chartDefinition, canvasFactory);
                case ChartType.StackedBar:
                case ChartType.StackedBar100Percent:
                    return new BarChartRenderer(chartDefinition, canvasFactory);
                case ChartType.Survival:
                    return new SurvivalChartRenderer(chartDefinition, canvasFactory);
                case ChartType.Ties:
                    return new TiesChartRenderer(chartDefinition, canvasFactory);
                case ChartType.Xy:
                    return new XyChartRenderer(chartDefinition, canvasFactory);
                case ChartType.Xy0To1:
                    return new Xy0To1ChartRenderer(chartDefinition, canvasFactory);
                case ChartType.Xyr:
                    return new XyrChartRenderer(chartDefinition, canvasFactory);
                case ChartType.Xyz:
                    return new XyzChartRenderer(chartDefinition, canvasFactory);
                default:
                    return new NotSetChartRenderer(chartDefinition, canvasFactory);
            }
        }

        private static string Render(IChartRenderer ch)
        {
            EmfCanvas emfCanvas = (EmfCanvas)ch.Canvas;
            return RtfImageRenderer.ImageStreamToRtf(emfCanvas.DetachAndReturnImageStream(), (int)emfCanvas.Width, (int)emfCanvas.Height);
        }

        private static string Render(ChartRenderer ch)
        {
            EmfCanvas emfCanvas = (EmfCanvas)ch.Canvas;
            return RtfImageRenderer.ImageStreamToRtf(emfCanvas.DetachAndReturnImageStream(), (int)emfCanvas.Width, (int)emfCanvas.Height);
        }

        private static ScaleParameters CreateScaleParameters(ScaleType xScaleType, ScaleType yScaleType)
        {
            return new ScaleParameters
            {
                X = new AxisScaleParameters { ScaleType = xScaleType },
                Y = new AxisScaleParameters { ScaleType = yScaleType }
            };
        }
    }
}
