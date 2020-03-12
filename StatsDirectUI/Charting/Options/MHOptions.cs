using System;

namespace StatsDirect.Charting
{
    [Serializable]
    public class MHOptions : GenericOptions
    {
        public int k { get; }
        public double[,] o { get; }
        public double[] odw { get; }
        public string[] title { get; }
        public double rmh { get; }
        public double ll { get; }
        public double ul { get; }
        public double cco { get; }
        public double[] odr { get; }
        public double[] odrl { get; }
        public double[] odru { get; }
        public bool[] lerr { get; }
        public bool[] uerr { get; }
        public string cap { get; }
        public int pbias { get; }
        public string qid { get; }

        public override bool ShowLegendIsRelevant => false;

        public override void Accept(IChartOptionVisitor visitor)
        {
            visitor.Visit(this);
        }

        public MHOptions(int k, double[,] o, double[] odw, string[] title, double rmh, double ll, double ul, double cco, double[] odr, double[] odrl, double[] odru, bool[] lerr, bool[] uerr, string cap, int pbias, string qid)
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
