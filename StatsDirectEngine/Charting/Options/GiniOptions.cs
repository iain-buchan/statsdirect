using System;

namespace StatsDirect.Charting
{
    [Serializable]
    public class GiniOptions : GenericOptions
    {
        public GiniOptions(IChartPreferences chartPreferences)
            : base(chartPreferences)
        {
        }

        public override bool UsesChartTitle => true;

        public override bool UsesXAxisTitle => true;

        public override bool UsesYAxisTitle => true;

        public override bool ShowLegendIsRelevant => false;

        public override void Accept(IChartOptionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
