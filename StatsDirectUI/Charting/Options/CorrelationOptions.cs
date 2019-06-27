using System;

namespace StatsDirect.Charting
{
    [Serializable]
    public class CorrelationOptions : GenericOptions
    {
        public int k;
        public string[] title;
        public double[] odr;
        public double[] odrl;
        public double[] odru;
        public double[] gn;
        public CorrelationRowType[] pg;
        public string cap;
        public string qid;
        public Transformation xform;
        public bool isDifference;
        public override bool ShowLegendIsRelevant => false;

        public override void Accept(IChartOptionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public CorrelationOptions(int k, string[] title, double[] odr, double[] odrl, double[] odru, double[] gn, CorrelationRowType[] pg, string cap, string qid, Transformation xform, bool isDifference)
        {
            this.k = k;
            this.title = title;
            this.odr = odr;
            this.odrl = odrl;
            this.odru = odru;
            this.gn = gn;
            this.pg= pg;
            this.cap = cap;
            this.qid = qid;
            this.xform = xform;
            this.isDifference = isDifference;
        }
    }
}
