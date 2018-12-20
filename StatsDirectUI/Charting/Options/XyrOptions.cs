using StatsDirect.Builtins;
using System;

namespace StatsDirect.Charting
{
    [Serializable]
    public class XyrOptions : GenericOptions
    {
        public double[,] x;
        public double[,,] y;
        public int ng;
        public int[] gn;
        public int[,] nr;
        public double[] b;
        public double[] a;
        public string xtxt;
        public string ytxt;
        public string title;
        public string[] bnam;
        public MinMax minMax;

        public override bool ShowLegendIsRelevant => true;

        public override void Accept(IChartOptionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public XyrOptions(double[,] x, double[,,] y, int ng, int[] gn, int[,] nr, double[] b, double[] a, string xtxt, string ytxt, string title, string[] bnam, MinMax minMax)
            : base(false)
        {
            this.x = x;
            this.y = y;
            this.ng = ng;
            this.gn = gn;
            this.nr = nr;
            this.b = b;
            this.a = a;
            this.xtxt = xtxt;
            this.ytxt = ytxt;
            this.title = title;
            this.bnam = bnam;
            this.minMax = minMax;
    }
}
}
