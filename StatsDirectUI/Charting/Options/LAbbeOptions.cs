using System;

namespace StatsDirect.Charting.Options
{
    [Serializable]
    public class LAbbeOptions : AbstractGenericOptions
    {
        public int K { get; }
        public double[,] O { get; }
        public double Rmh { get; }
        public override bool ShowLegendIsRelevant => false;

        public override void Accept(IChartOptionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public LAbbeOptions(int k, double[,] o, double rmh, IChartPreferences chartPreferences)
            : base(chartPreferences)
        {
            K = k;
            O = o;
            Rmh = rmh;
        }
    }
}
