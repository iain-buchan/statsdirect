using System;

namespace StatsDirect.Charting
{
    [Serializable]
    public class LAbbeOptions : GenericOptions
    {
        public int k;
        public double[,] o;
        public double rmh;
        public override bool ShowLegendIsRelevant => false;

        public override void Accept(IChartOptionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public LAbbeOptions(IChartPreferences chartPreferences, int k, double[,] o, double rmh)
            : base(chartPreferences)
        {
            this.k = k;
            this.o = o;
            this.rmh = rmh;
        }
    }
}
