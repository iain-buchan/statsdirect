using System;

namespace StatsDirect.Charting
{
    [Serializable]
    public class XyOptions : GenericOptions
    {
        public double[] x { get; }
        public double[] y { get; }
        public bool zPlot { get; }
        public DataMinMax minMaxY { get; }
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
            XAxisTitle = xtxt;
            YAxisTitle = ytxt;
            Title = title;
            this.zPlot = zPlot;
        }
    }
}
