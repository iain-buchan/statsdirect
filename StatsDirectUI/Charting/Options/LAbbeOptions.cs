using System;

namespace StatsDirect.Charting
{
    [Serializable]
    public class LAbbeOptions : GenericOptions
    {
        public int k;
        public double[,] o;
        public double rmh;
        /// <summary>
        /// True when rmh is a pooled odds ratio, whose locus on the plot is a curve; false when it is a pooled relative risk, whose locus is a line through the origin.
        /// </summary>
        public bool isOddsRatio;
        public override bool ShowLegendIsRelevant => false;

        public override void Accept(IChartOptionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public LAbbeOptions(int k, double[,] o, double rmh, bool isOddsRatio = false)
        {
            this.k = k;
            this.o = o;
            this.rmh = rmh;
            this.isOddsRatio = isOddsRatio;
        }
    }
}
