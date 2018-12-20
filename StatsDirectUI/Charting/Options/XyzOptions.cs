using System;

namespace StatsDirect.Charting
{
    [Serializable]
    public class XyzOptions : GenericOptions
    {
        public double[] x;
        public double[] y;
        public double[] z;
        public string xtxt;
        public string ytxt;
        public string title;
        public bool zPlot;
        public DataMinMax minMaxY;
        public override bool ShowLegendIsRelevant => false;

        public override void Accept(IChartOptionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public XyzOptions(double[] x, double[] y, double[] z, string xtxt, string ytxt, string title, bool zPlot, DataMinMax minMaxY)
            : base(false)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.xtxt = xtxt;
            this.ytxt = ytxt;
            this.title = title;
            this.zPlot = zPlot;
        }
    }
}
