using System;

namespace StatsDirect.Charting.Options
{
    [Serializable]
    public class AgreementOptions : AbstractGenericOptions
    {
        public double[] Av { get; }
        public bool HasLimits { get; }
        public double Lla { get; }
        public double Mean { get; }
        public double[] Mxd { get; }
        public double P0 { get; }
        public double Ula { get; }

        public AgreementOptions(IChartPreferences chartPreferences)
            : base(chartPreferences)
        {
        }

        public override bool ShowLegendIsRelevant => false;
        public override bool UsesShowLegend => false;

        public override void Accept(IChartOptionVisitor visitor)
        {
            visitor.Visit(this);
        }
    }
}
