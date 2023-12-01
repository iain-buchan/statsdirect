using System.Collections.Generic;
using StatsDirect.Numerics;
using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    ///  <summary>
    ///  Holds all the data for a ChartRenderer to be able to render a chart with particular data and options.
    ///  </summary>
    ///  <remarks>Immutable.</remarks>
    public class ChartDefinition : IFillable, IRenderable
    {
        private ScaleParameters? scaleParameters;

        public IReadOnlyList<ISeries> XSeries { get; }
        public IReadOnlyList<ISeries> YSeries { get; }

        public double DataMinX { get; private set; }
        public double DataMinGreaterThanZeroX { get; private set; }
        public double DataMaxX { get; private set; }
        public double DataMinY { get; private set; }
        public double DataMinGreaterThanZeroY { get; private set; }
        public double DataMaxY { get; private set; }

        ///  <summary>
        ///  The type of chart to be plotted.
        ///  </summary>
        public ChartType ChartType { get; }

        public AbstractChartOptions ChartOptions { get; }

        public bool IsAscii => ChartOptions.IsAscii;

        public ChartDefinition(ChartType chartType,
            AbstractChartOptions chartOptions,
            IReadOnlyList<ISeries> xSeries,
            IReadOnlyList<ISeries> ySeries,
            ScaleParameters? scaleParameters = default)
        {
            ChartType = chartType;
            ChartOptions = chartOptions;
            XSeries = xSeries;
            YSeries = ySeries;
            this.scaleParameters = scaleParameters;
            CheckData();
        }

        public ChartDefinition(ChartDefinition template,
            ChartType? chartType = default,
            AbstractChartOptions? chartOptions = default,
            IReadOnlyList<ISeries>? xSeries = default,
            IReadOnlyList<ISeries>? ySeries = default,
            ScaleParameters? scaleParameters = default)
        {
            ChartType = chartType ?? template.ChartType;
            ChartOptions = chartOptions ?? template.ChartOptions;
            XSeries = xSeries ?? template.XSeries;
            YSeries = ySeries ?? template.YSeries;
            this.scaleParameters = scaleParameters ?? template.scaleParameters;
            CheckData();
        }

        private void CheckData()
        {
            // Check XSeries Data
            DataMaxX = double.MinValue;
            DataMinX = double.MaxValue;
            DataMinGreaterThanZeroX = double.MaxValue;
            foreach (ISeries series in XSeries)
                if (series is DoubleSeries s)
                    foreach (double q in s.Data)
                        if (q != Constant.MISSING)
                        {
                            if (q < DataMinX)
                                DataMinX = q;
                            if (q < DataMinGreaterThanZeroX && q > 0)
                                DataMinGreaterThanZeroX = q;
                            if (q > DataMaxX)
                                DataMaxX = q;
                        }

            // Check YSeries Data
            DataMaxY = double.MinValue;
            DataMinY = double.MaxValue;
            DataMinGreaterThanZeroY = double.MaxValue;
            foreach (ISeries series in YSeries)
                if (series is DoubleSeries s)
                    foreach (double q in s.Data)
                        if (q != Constant.MISSING)
                        {
                            if (q < DataMinY)
                                DataMinY = q;
                            if (q < DataMinGreaterThanZeroY && q > 0)
                                DataMinGreaterThanZeroY = q;
                            if (q > DataMaxY)
                                DataMaxY = q;
                        }
        }

        public bool HasScaleParameters => scaleParameters is not null;

        public ScaleParameters ScaleParameters
        {
            get => scaleParameters ??= GetScaleParameters();
            set => scaleParameters = value;
        }

        private ScaleParameters GetScaleParameters()
        {
            using IChartRenderer renderer = ChartRendererFactory.ChartRendererFor(this, null);
            return renderer.GetScaleParameters();
        }

        void IRenderable.Accept(IRenderableVisitor visitor)
        {
            visitor.Visit(this);
        }

        public string FillerToUse => "ChartOptions";
    }
}
