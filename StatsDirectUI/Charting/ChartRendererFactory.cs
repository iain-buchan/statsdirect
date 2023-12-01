using StatsDirect.Charting.Renderer;
using StatsDirect.Templates;
using System;

namespace StatsDirect.Charting
{
    /// <summary>
    /// Knows where to get the correct implementation of IChartRenderer for any given definition.
    /// </summary>
    public class ChartRendererFactory : IChartRendererFactory
    {
        private static ICanvasFactory NULL_FACTORY { get; } = new NullCanvasFactory();

        private ISdPreferences SdPreferences { get; }
        private IUserInterface UserInterface { get; }

        public ChartRendererFactory(ISdPreferences sdPreferences, IUserInterface userInterface)
        {
            SdPreferences = sdPreferences;
            UserInterface = userInterface;
        }

        ChartDefinition IChartRendererFactory.PrepForLater(ChartType chartType, AbstractChartOptions options, DoubleSeries? xSeries, DoubleSeries? ySeries)
        {
            ChartDefinition cd = new(
                chartType,
                options,
                xSeries is null
                    ? Array.Empty<DoubleSeries>()
                    : new DoubleSeries[] { xSeries },
                ySeries is null
                    ? Array.Empty<DoubleSeries>()
                    : new DoubleSeries[] { ySeries }
            );
            // Called from code, and there's no other path for getting hold of the scale parameters, so force that here.
            _ = cd.ScaleParameters;
            return cd;
        }

        internal ParameterBag PlotForResultsOnly(ChartDefinition definition)
        {
            using IChartRenderer ch = ((IChartRendererFactory)this).ChartRendererFor(definition, NULL_FACTORY);
            return ch.Plot(true);
        }

        IChartRenderer IChartRendererFactory.ChartRendererFor(ChartDefinition chartDefinition, ICanvasFactory canvasFactory) =>
            chartDefinition.ChartType switch
            {
                ChartType.AgreementPair => new AgreementPairChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.Bar => new BarChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.BiasMA => new BiasMAChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.BoxWhisker => new BoxWhiskerChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.Control => new ControlChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.Correlation => new CorrelationChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.CoxSurvivalOrHazard => new CoxSurvivalOrHazardChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.Cox2 => new Cox2ChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.Effect => new EffectChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.ErrorBar => new ErrorBarChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.Forest => new ForestChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.Gini => new GiniChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.Histogram => new HistogramChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.KaplanMeier => new KaplanMeierChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.LAbbe => new LAbbeChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.Ladder => new LadderChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.LinearizedEstimation => new LinearizedEstimationChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.LinearRegression => new LinearRegressionChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.LinearRegressionAndMaybeSeCiOrPredictionInterval => new LinearRegressionAndMaybeSeCiOrPredictionIntervalChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.LineXY => new ScatterChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.Logit => new LogitChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.MH => new MHChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.MHRD => new MHRDChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.Normal => new NormalChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.PolynomialRegression => new PolynomialRegressionChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.Pyramid => new PyramidChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.ROC => new RocChartRenderer(chartDefinition, canvasFactory, SdPreferences, UserInterface),
                ChartType.ScatterXY => new ScatterChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.Spread => new SpreadChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.StackedBar or
                ChartType.StackedBar100Percent => new BarChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.Survival => new SurvivalChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.Ties => new TiesChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.Xy => new XyChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.Xy0To1 => new Xy0To1ChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.Xyr => new XyrChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                ChartType.Xyz => new XyzChartRenderer(chartDefinition, canvasFactory, SdPreferences),
                _ => new NotSetChartRenderer(),
            };
    }
}
