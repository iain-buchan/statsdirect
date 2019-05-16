using System;

namespace StatsDirect.Charting
{
    [Serializable]
    public class EffectOptions : GenericOptions
    {
        public EffectOptions(bool useColour)
            : base(useColour)
        {
        }

        public int k;
        public double[] cn;
        public double[] en;
        public string[] title;
        public double rmh;
        public double ll;
        public double ul;
        public double cco;
        public double[] odr;
        public double[] odrl;
        public double[] odru;
        public string cap;
        public int pbias;
        public string qid;

        public override bool ShowLegendIsRelevant => false;

        public override void Accept(IChartOptionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public EffectOptions(int k, double[] cn, double[] en, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, string cap, int pbias, string qid, bool useColour)
            : base(useColour)
        {
            this.k = k;
            this.cn = cn;
            this.en = en;
            this.title = title;
            this.rmh = rmh;
            this.ll = ll;
            this.ul = ul;
            this.cco = cco;
            this.odr = odr;
            this.odrl = odrl;
            this.odru = odru;
            this.cap = cap;
            this.pbias = pbias;
            this.qid = qid;
    }
}
}
