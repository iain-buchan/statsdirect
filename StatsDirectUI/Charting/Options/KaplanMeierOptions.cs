using System;

namespace StatsDirect.Charting
{
    [Serializable]
    public class KaplanMeierOptions : GenericOptions
    {
        public int[,] dead;
        public int groups;
        public int[] cnx;
        public string[] glab;
        public bool tic;
        public bool marker;
        public double[,] x;
        public double[,] y;
        public KaplanMeierPlotMode plotMode;
        public string xAxisTitle;
        public string yAxisTitle;
        public string title;
        public override bool ShowLegendIsRelevant => true;

        public override void Accept(IChartOptionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public KaplanMeierOptions(int[,] dead, int groups, int[] cnx, string[] glab, bool tic, bool marker, double[,] x, double[,] y, KaplanMeierPlotMode plotMode, string xAxisTitle, string yAxisTitle, string title)
            : base(true)
        {
            this.dead = dead;
            this.groups = groups;
            this.cnx = cnx;
            this.glab = glab;
            this.tic = tic;
            this.marker = marker;
            this.x = x;
            this.y = y;
            this.plotMode = plotMode;
            this.xAxisTitle = xAxisTitle;
            this.yAxisTitle = yAxisTitle;
            this.title = title;
        }
    }
}
