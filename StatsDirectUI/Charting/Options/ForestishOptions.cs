using System.Collections.Generic;

namespace StatsDirect.Charting.Options
{
    public abstract class ForestishOptions : AbstractGenericOptions, IMarkerTypes
    {
        public double Cco { get; }
        public FillStyle? ForcedFillStyle { get; }
        public bool? ForcedIsFilled { get; }
        public IReadOnlyList<double> GroupSizes { get; }
        public int K { get; }
        public bool MarkCentres { get; } = true;
        public IReadOnlyList<MarkerType> MarkerTypes { get; }
        public IReadOnlyList<double> OddsRatioLcis { get; }
        public IReadOnlyList<double> OddsRatios { get; }
        public IReadOnlyList<double> OddsRatioUcis { get; }
        public IReadOnlyList<SeriesOptionsDescriptor> SeriesOptions { get; }
        public IReadOnlyList<string> Titles { get; }

        protected ForestishOptions(IChartPreferences chartPreferences,
            double cco,
            IReadOnlyList<double> groupSizes,
            int k,
            bool markCentres,
            IReadOnlyList<MarkerType> markerTypes,
            IReadOnlyList<double> oddsRatioLcis,
            IReadOnlyList<double> oddsRatios,
            IReadOnlyList<double> oddsRatioUcis,
            IReadOnlyList<SeriesOptionsDescriptor> seriesOptions,
            IReadOnlyList<string> titles
            )
            : base(chartPreferences)
        {
            Cco = cco;
            GroupSizes = groupSizes;
            K = k;
            MarkCentres = markCentres;
            MarkerTypes = markerTypes;
            OddsRatioLcis = oddsRatioLcis;
            OddsRatios = oddsRatios;
            OddsRatioUcis = oddsRatioUcis;
            SeriesOptions = seriesOptions;
            Titles = titles;

            //  A forest plot has one marker for the study and a second for the pooled effect
            MarkerTypes = new[]
            {
                new MarkerType(
                    MarkerType.Default,
                    true,
                    ColorDescriptor.Black,
                    DashStyleDescriptor.Solid,
                    ColorDescriptor.Gray,
                    markerShape: MarkerShape.Square
                ),
            };
            MarkerType pooledMarkerType = new(
                MarkerType.Default,
                true,
                ColorDescriptor.Black,
                DashStyleDescriptor.Solid,
                ColorDescriptor.Gray,
                markerShape: MarkerShape.Diamond
            );

            SeriesOptions = new[]
            {
                new SeriesOptionsDescriptor()
                {
                    SeriesName = "Study",
                    AllowChangeToDashStyle = false,
                    AllowChangeToMarkerSize = false,
                    AllowChangeToLineThickness = false,
                    MarkerIndex = 0
                },
                new SeriesOptionsDescriptor()
                {
                    SeriesName = "Pooled effect",
                    AllowChangeToDashStyle = false,
                    AllowChangeToMarkerSize = false,
                    AllowChangeToLineThickness = false,
                    MarkerIndex = 1
                }
            };
        }

        public override bool UsesShowLegend => false;

        public override bool ShowLegendIsRelevant => false;
    }
}
