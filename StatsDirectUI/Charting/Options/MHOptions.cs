using System;

namespace StatsDirect.Charting
{
    [Serializable]
    public class MHOptions : GenericOptions
    {
        public int k;
        public double[,] o;
        public double[] odw;
        public string[] title;
        public double rmh;
        public double ll;
        public double ul;
        public double cco;
        public double[] odr;
        public double[] odrl;
        public double[] odru;
        public bool[] lerr;
        public bool[] uerr;
        public string cap;
        public int pbias;
        public string qid;
        public override bool ShowLegendIsRelevant => false;

        public override void Accept(IChartOptionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public MHOptions(int k, double[,] o, double[] odw, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, bool[] lerr, bool[] uerr, string cap, int pbias, string qid, bool useColour)
            : base(useColour)
        {
            this.k = k;
            this.o = o;
            this.odw = odw;
            this.title = title;
            this.rmh = rmh;
            this.ll = ll;
            this.ul = ul;
            this.cco = cco;
            this.odr = odr;
            this.odrl = odrl;
            this.odru = odru;
            this.lerr = lerr;
            this.uerr = uerr;
            this.cap = cap;
            this.pbias = pbias;
            this.qid = qid;
        }
    }
}
