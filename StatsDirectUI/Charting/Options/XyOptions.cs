using System;

namespace StatsDirect.Charting
{
    [Serializable]
    public class XyOptions : GenericOptions
    {
        public double[] x;
        public double[] y;
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

        public XyOptions(double[] x, double[] y, string xtxt, string ytxt, string title, bool zPlot, DataMinMax minMaxY)
            : base(false)
        {
            this.x = x;
            this.y = y;
            this.xtxt = xtxt;
            this.ytxt = ytxt;
            this.title = title;
            this.zPlot = zPlot;
        }
    }
}
