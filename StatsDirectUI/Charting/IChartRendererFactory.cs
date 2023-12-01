using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    public interface IChartRendererFactory
    {
        IChartRenderer ChartRendererFor(ChartDefinition chartDefinition, ICanvasFactory canvasFactory);
        ChartDefinition PrepForLater(ChartType chartType, AbstractChartOptions options, DoubleSeries? xSeries = default, DoubleSeries? ySeries = default);
    }
}